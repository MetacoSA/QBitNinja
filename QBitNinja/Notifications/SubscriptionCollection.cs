using QBitNinja.Models;
using System.Collections.Generic;
using System.Linq;

namespace QBitNinja.Notifications
{
    public class SubscriptionCollection
    {
        private List<Subscription> _Subscriptions;

        public SubscriptionCollection(IEnumerable<Subscription> subscriptions)
        {
            this._Subscriptions = new List<Subscription>(subscriptions);
        }
        public IEnumerable<NewBlockSubscription> GetNewBlocks()
        {
            return _Subscriptions.OfType<NewBlockSubscription>();
        }

        public IEnumerable<NewTransactionSubscription> GetNewTransactions()
        {
            return _Subscriptions.OfType<NewTransactionSubscription>();
        }

        public int Count
        {
            get
            {
                return _Subscriptions.Count;
            }
        }

        internal void Add(Subscription subscription)
        {
            _Subscriptions.Add(subscription);
        }

        internal void Remove(string id)
        {
            _Subscriptions.RemoveAll(s => s.Id == id);
        }
    }
}
