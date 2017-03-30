using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace QBitNinja
{
    public class UpdateChainListener : IDisposable
    {
        IDisposable _Subscription;
        public UpdateChainListener()
        {

        }

        internal Timer Timer
        {
            get;
            set;
        }

        internal QBitNinjaDependencyResolver Resolver
        {
            get;
            set;
        }

        public void Listen(HttpConfiguration config)
        {
            Resolver = (QBitNinjaDependencyResolver)config.DependencyResolver;

            Timer = new Timer(_ => Resolver.UpdateChain());
            Timer.Change(0, (int)TimeSpan.FromSeconds(30).TotalMilliseconds);

            var conf = Resolver.Get<QBitNinjaConfiguration>();
            _Subscription =
                conf.Topics
                .NewBlocks
                .CreateConsumer("webchain", true)
                .EnsureSubscriptionExists()
                .OnMessage(b =>
                {
                    Resolver.UpdateChain();
                });
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (Timer != null)
                Timer.Dispose();
            if (_Subscription != null)
                _Subscription.Dispose();
        }

        #endregion
    }
}
