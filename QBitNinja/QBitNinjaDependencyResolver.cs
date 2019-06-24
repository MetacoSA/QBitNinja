using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Web.Http.Dependencies;
using Autofac;
using Autofac.Integration.WebApi;
using NBitcoin;
using NBitcoin.Indexer;

namespace QBitNinja
{
    public class QBitNinjaDependencyResolver : IDependencyResolver
    {
        private class AutoFacDependencyScope : IDependencyScope
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
                return _lifetimeScope.TryResolve(serviceType, out object result)
                           ? result
                           : _dependencyScope.GetService(serviceType);
            }

            public IEnumerable<object> GetServices(Type serviceType)
            {
                object service = GetService(serviceType);
                return service == null
                           ? _dependencyScope.GetServices(serviceType)
                           : new[] { service };
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

        private readonly IDependencyResolver _defaultResolver;
        private readonly IContainer _container;

        public QBitNinjaDependencyResolver(QBitNinjaConfiguration configuration, IDependencyResolver defaultResolver)
        {
            _defaultResolver = defaultResolver;
            ContainerBuilder builder = new ContainerBuilder();
            builder.Register(ctx => configuration).SingleInstance();
            builder.Register(ctx => configuration.Indexer.CreateIndexerClient());
            builder.Register(ctx =>
            {
                IndexerClient client = ctx.Resolve<IndexerClient>();
                ConcurrentChain chain = new ConcurrentChain(configuration.Indexer.Network);
                LoadCache(chain, configuration.LocalChain, configuration.Indexer.Network);
                IEnumerable<ChainBlockHeader> changes = client.GetChainChangesUntilFork(chain.Tip, false);
                try
                {
                    changes.UpdateChain(chain);
                }
                catch (ArgumentException) // Happen when chain in table is corrupted
                {
                    client.Configuration.GetChainTable().DeleteIfExists();
                    for (var i = 0; i < 20; i++)
                    {
                        try
                        {
                            if (client.Configuration.GetChainTable().CreateIfNotExists())
                            {
                                break;
                            }
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

        private static void LoadCache(ConcurrentChain chain, string cacheLocation, Network network)
        {
            if (string.IsNullOrEmpty(cacheLocation))
            {
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(cacheLocation);
                chain.Load(bytes, network);
            }
            catch //We don't care if it don't succeed
            {
            }
        }

        private static void SaveChainCache(ConcurrentChain chain, string cacheLocation)
        {
            if (string.IsNullOrEmpty(cacheLocation))
            {
                return;
            }

            try
            {
                FileInfo cacheFile = new FileInfo(cacheLocation);
                FileInfo cacheFileNew = new FileInfo(cacheLocation + ".new");
                using (FileStream fs = new FileStream(
                    cacheFileNew.FullName,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    1024 * 1024 * 5))
                {
                    chain.WriteTo(fs);
                }

                if (cacheFile.Exists
                    && cacheFile.Length >= cacheFileNew.Length)
                {
                    cacheFileNew.Delete();
                }
                else
                {
                    if (cacheFile.Exists)
                    {
                        cacheFile.Delete();
                    }

                    cacheFileNew.MoveTo(cacheLocation);
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
            return _container.TryResolve(serviceType, out object result)
                       ? result
                       : _defaultResolver.GetService(serviceType);
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            object service = GetService(serviceType);
            return service == null
                       ? _defaultResolver.GetServices(serviceType)
                       : new[] { service };
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            SaveChainCache(Get<ConcurrentChain>(), Get<QBitNinjaConfiguration>().LocalChain);
            _container.Dispose();

        }

        #endregion

        public bool UpdateChain()
        {
            IndexerClient client = Get<IndexerClient>();
            ConcurrentChain chain = Get<ConcurrentChain>();
            uint256 oldTip = chain.Tip.HashBlock;
            IEnumerable<ChainBlockHeader> changes = client.GetChainChangesUntilFork(chain.Tip, false);
            changes.UpdateChain(chain);
            uint256 newTip = chain.Tip.HashBlock;
            return newTip != oldTip;
        }
    }
}
