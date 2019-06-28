using Autofac;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web.Http.Dependencies;
using Autofac.Integration.WebApi;
using NBitcoin;
using NBitcoin.Indexer;
using System.IO;
using System.Threading;
using Microsoft.WindowsAzure.Storage.Table;
using System.Threading.Tasks;

namespace QBitNinja
{
	public class QBitNinjaDependencyResolver : IDependencyResolver
	{
		class AutoFacDependencyScope : IDependencyScope
		{
			private readonly ILifetimeScope _lifetimeScope;
			private readonly IDependencyScope _dependencyScope;

			public AutoFacDependencyScope(ILifetimeScope lifetimeScope, IDependencyScope dependencyScope)
			{
				_lifetimeScope = lifetimeScope;
				_dependencyScope = dependencyScope;
			}

			#region IDependencyScope Members

			public object GetService(Type serviceType)
			{
				object result;
				return _lifetimeScope.TryResolve(serviceType, out result) ? result : _dependencyScope.GetService(serviceType);
			}

			public IEnumerable<object> GetServices(Type serviceType)
			{
				var service = GetService(serviceType);
				return service == null ? _dependencyScope.GetServices(serviceType) : new[] { service };
			}


			#endregion

			#region IDisposable Members

			public void Dispose()
			{
				_lifetimeScope.Dispose();
				_dependencyScope.Dispose();
			}

			#endregion
		}

		readonly IDependencyResolver _defaultResolver;
		readonly IContainer _container;

		public QBitNinjaDependencyResolver(QBitNinjaConfiguration configuration, IDependencyResolver defaultResolver)
		{
			_defaultResolver = defaultResolver;
			ContainerBuilder builder = new ContainerBuilder();
            ChainSynchronizeStatus chainStatus = new ChainSynchronizeStatus();
            builder.Register(ctx => chainStatus).SingleInstance();
            builder.Register(ctx => configuration).SingleInstance();
			builder.Register(ctx => configuration.Indexer.CreateIndexerClient());
			builder.Register(ctx =>
			{
				var client = ctx.Resolve<IndexerClient>();
				ConcurrentChain chain = new ConcurrentChain(configuration.Indexer.Network);
                _ = LoadChain(configuration, client, chain, chainStatus);
                return chain;
			}).SingleInstance();
			builder.RegisterApiControllers(Assembly.GetExecutingAssembly());
			_container = builder.Build();
		}

        private async Task LoadChain(QBitNinjaConfiguration configuration, IndexerClient client, ConcurrentChain chain, ChainSynchronizeStatus status)
        {
            await Task.Delay(1).ConfigureAwait(false);
            LoadCache(chain, configuration.LocalChain, configuration.Indexer.Network);
            status.FileCachedHeight = chain.Height;
            var changes = client.GetChainChangesUntilFork(chain.Tip, false);
            try
            {
                await changes.UpdateChain(chain, _Cts.Token);
            }
            catch (ArgumentException) //Happen when chain in table is corrupted
            {
                client.Configuration.GetChainTable().DeleteIfExists();
                for (int i = 0; i < 20; i++)
                {
                    try
                    {
                        if (client.Configuration.GetChainTable().CreateIfNotExists())
                            break;
                    }
                    catch
                    {
                    }
                    await Task.Delay(10000);
                }
                status.ReindexHeaders = true;
                await client.Configuration.CreateIndexer().IndexChain(chain, _Cts.Token);
            }
            status.TableFetchedHeight = chain.Height;
            SaveChainCache(chain, configuration.LocalChain);
            status.Synchronizing = false;
            Interlocked.Decrement(ref _UpdateChain);
        }

        private static void LoadCache(ConcurrentChain chain, string cacheLocation, Network network)
		{
			if(string.IsNullOrEmpty(cacheLocation))
				return;
			try
			{
				var bytes = File.ReadAllBytes(cacheLocation);
				chain.Load(bytes, network);
			}
			catch //We don't care if it don't succeed
			{
			}
		}

		private static void SaveChainCache(ConcurrentChain chain, string cacheLocation)
		{
			if(string.IsNullOrEmpty(cacheLocation))
			{
				return;
			}
			try
			{
				var cacheFile = new FileInfo(cacheLocation);
				var cacheFileNew = new FileInfo(cacheLocation + ".new");
				using(var fs = new FileStream(cacheFileNew.FullName, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024 * 5))
				{
					chain.WriteTo(fs);
				}
				if(!cacheFile.Exists || cacheFile.Length < cacheFileNew.Length)
				{
					if(cacheFile.Exists)
						cacheFile.Delete();
					cacheFileNew.MoveTo(cacheLocation);
				}
				else
					cacheFileNew.Delete();
			}
			catch //Don't care if fail
			{
			}
		}

		#region IDependencyResolver Members

		public IDependencyScope BeginScope()
		{
			return new AutoFacDependencyScope(_container.BeginLifetimeScope(), _defaultResolver.BeginScope());
		}

		#endregion

		#region IDependencyScope Members

		public T Get<T>()
		{
			return (T)GetService(typeof(T));
		}

		public object GetService(Type serviceType)
		{
			object result;

			return _container.TryResolve(serviceType, out result) ? result : _defaultResolver.GetService(serviceType);
		}

		public IEnumerable<object> GetServices(Type serviceType)
		{
			var service = GetService(serviceType);
			return service == null ? _defaultResolver.GetServices(serviceType) : new[] { service };
		}

        #endregion

        #region IDisposable Members
        CancellationTokenSource _Cts = new CancellationTokenSource();
		public void Dispose()
		{
            _Cts.Cancel();
            SaveChainCache(Get<ConcurrentChain>(), Get<QBitNinjaConfiguration>().LocalChain);
			_container.Dispose();
        }

        #endregion

        int _UpdateChain = 1;
		public async Task<bool> UpdateChain()
		{
            if (Interlocked.CompareExchange(ref _UpdateChain, 1, 0) != 0)
                return false;
            try
            {
                var client = Get<IndexerClient>();
                var chain = Get<ConcurrentChain>();
                var oldTip = chain.Tip.HashBlock;
                var changes = client.GetChainChangesUntilFork(chain.Tip, false, _Cts.Token);
                await changes.UpdateChain(chain, _Cts.Token);
                var newTip = chain.Tip.HashBlock;
                return newTip != oldTip;
            }
            finally
            {
                Interlocked.Decrement(ref _UpdateChain);
            }
        }
	}
}
