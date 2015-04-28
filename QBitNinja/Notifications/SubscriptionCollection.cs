using QBitNinja.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Notifications
{
    public class SubscriptionCollection
    {
        private Subscription[] newBlockSubscription;

        public SubscriptionCollection(Subscription[] subscriptions)
        {
            this.newBlockSubscription = subscriptions;
        }
        public IEnumerable<NewBlockSubscription> GetNewBlocks()
        {
            return newBlockSubscription.OfType<NewBlockSubscription>();
        }

        public IEnumerable<NewTransactionSubscription> GetNewTransactions()
        {
            return newBlockSubscription.OfType<NewTransactionSubscription>();
        }

        public int Count
        {
            get
            {
                return newBlockSubscription.Length;
            }
        }
    }
}
