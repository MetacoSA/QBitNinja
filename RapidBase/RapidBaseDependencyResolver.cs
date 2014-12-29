using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Dependencies;
using Autofac.Integration.WebApi;
using NBitcoin;
using NBitcoin.Indexer;

namespace RapidBase
{
    public class RapidBaseDependencyResolver : IDependencyResolver
    {
        class AutoFacDependencyScope : IDependencyScope
        {
            private ILifetimeScope lifetimeScope;
            private IDependencyScope dependencyScope;

            public AutoFacDependencyScope(ILifetimeScope lifetimeScope, IDependencyScope dependencyScope)
            {
                this.lifetimeScope = lifetimeScope;
                this.dependencyScope = dependencyScope;
            }

            #region IDependencyScope Members

            public object GetService(Type serviceType)
            {
                object result = null;
                if (lifetimeScope.TryResolve(serviceType, out result))
                {
                    return result;
                }
                return dependencyScope.GetService(serviceType);
            }

            public IEnumerable<object> GetServices(Type serviceType)
            {
                var service = GetService(serviceType);
                if (service == null)
                    return dependencyScope.GetServices(serviceType);
                return new[] { service };
            }


            #endregion

            #region IDisposable Members

            public void Dispose()
            {
                this.lifetimeScope.Dispose();
                this.dependencyScope.Dispose();
            }

            #endregion
        }
        IDependencyResolver _DefaultResolver;
        IContainer _Container;
        public RapidBaseDependencyResolver(RapidBaseConfiguration configuration, IDependencyResolver defaultResolver)
        {
            this._DefaultResolver = defaultResolver;
            ContainerBuilder builder = new ContainerBuilder();
            builder.Register<RapidBaseConfiguration>(ctx => configuration).SingleInstance();
            builder.Register<IndexerClient>(ctx => configuration.Indexer.CreateIndexerClient());
            builder.Register<ConcurrentChain>(ctx =>
            {
                var client = ctx.Resolve<IndexerClient>();
                ConcurrentChain chain = new ConcurrentChain(configuration.Indexer.Network);
                var changes = client.GetChainChangesUntilFork(chain.Tip, false);
                changes.UpdateChain(chain);
                return chain;
            }).SingleInstance();
            builder.RegisterApiControllers(Assembly.GetExecutingAssembly());
            _Container = builder.Build();
        }


        #region IDependencyResolver Members

        public IDependencyScope BeginScope()
        {
            return new AutoFacDependencyScope(_Container.BeginLifetimeScope(), _DefaultResolver.BeginScope());
        }

        #endregion

        #region IDependencyScope Members

        public T Get<T>()
        {
            return (T)GetService(typeof(T));
        }

        public object GetService(Type serviceType)
        {
            object result = null;
            if (_Container.TryResolve(serviceType, out result))
            {
                return result;
            }
            return _DefaultResolver.GetService(serviceType);
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            var service = GetService(serviceType);
            if (service == null)
                return _DefaultResolver.GetServices(serviceType);
            return new[] { service };
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            _Container.Dispose();
        }

        #endregion

        public void UpdateChain()
        {
            var client = Get<IndexerClient>();
            var chain = Get<ConcurrentChain>();
            var changes = client.GetChainChangesUntilFork(chain.Tip, false);
            changes.UpdateChain(chain);
        }
    }
}
