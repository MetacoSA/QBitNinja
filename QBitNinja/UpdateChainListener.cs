using System;
using System.Threading;
using System.Web.Http;

namespace QBitNinja
{
    public class UpdateChainListener : IDisposable
    {
        private IDisposable _Subscription;

        internal Timer Timer { get; set; }

        internal QBitNinjaDependencyResolver Resolver { get; set; }

        public void Listen(HttpConfiguration config)
        {
            Resolver = (QBitNinjaDependencyResolver)config.DependencyResolver;

            Timer = new Timer(_ => Resolver.UpdateChain());
            Timer.Change(0, (int)TimeSpan.FromSeconds(30).TotalMilliseconds);

            QBitNinjaConfiguration conf = Resolver.Get<QBitNinjaConfiguration>();
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
            Timer?.Dispose();
            _Subscription?.Dispose();
        }

        #endregion
    }
}
