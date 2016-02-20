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
            builder.Register(ctx => configuration).SingleInstance();
            builder.Register(ctx => configuration.Indexer.CreateIndexerClient());
            builder.Register(ctx =>
            {
                var client = ctx.Resolve<IndexerClient>();
                ConcurrentChain chain = new ConcurrentChain(configuration.Indexer.Network);
                LoadCache(chain, configuration.LocalChain);
                var changes = client.GetChainChangesUntilFork(chain.Tip, false);
                try
                {
                    changes.UpdateChain(chain);
                }
                catch(ArgumentException) //Happen when chain in table is corrupted
                {
                    client.Configuration.GetChainTable().DeleteIfExists();
                    for(int i = 0; i < 20; i++)
                    {
                        try
                        {
                            if(client.Configuration.GetChainTable().CreateIfNotExists())
                                break;
                        }
                        catch
                        {
                        }
                        Thread.Sleep(10000);
                    }
                    client.Configuration.CreateIndexer().IndexChain(chain);
                }
                SaveChainCache(chain, configuration.LocalChain);
                return chain;
            }).SingleInstance();
            builder.RegisterApiControllers(Assembly.GetExecutingAssembly());
            _container = builder.Build();
        }

        private static void LoadCache(ConcurrentChain chain, string cacheLocation)
        {
            if (string.IsNullOrEmpty(cacheLocation))
                return;
            try
            {
                var bytes = File.ReadAllBytes(cacheLocation);
                chain.Load(bytes);
            }
            catch //We don't care if it don't succeed
            {
            }
        }

        private static void SaveChainCache(ConcurrentChain chain, string cacheLocation)
        {
            if (string.IsNullOrEmpty(cacheLocation))
                return;
            try
            {
                var file = new FileInfo(cacheLocation);
                if (!file.Exists || (DateTime.UtcNow - file.LastWriteTimeUtc) > TimeSpan.FromDays(1))
                    using (var fs = File.Open(cacheLocation, FileMode.Create))
                    {
                        chain.WriteTo(fs);
                    }
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

        const string LocalChainLocation = "LocalChain.dat";
        public void Dispose()
        {
            SaveChainCache(Get<ConcurrentChain>(), Get<QBitNinjaConfiguration>().LocalChain);
            _container.Dispose();

        }

        #endregion

        public bool UpdateChain()
        {
            var client = Get<IndexerClient>();
            var chain = Get<ConcurrentChain>();
            var oldTip = chain.Tip.HashBlock;
            var changes = client.GetChainChangesUntilFork(chain.Tip, false);
            changes.UpdateChain(chain);
            var newTip = chain.Tip.HashBlock;
            return newTip != oldTip;
        }
    }
}
