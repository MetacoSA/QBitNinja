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
        private NewBlockSubscription[] newBlockSubscription;

        public SubscriptionCollection(NewBlockSubscription[] subscriptions)
        {
            this.newBlockSubscription = subscriptions;
        }
        public IEnumerable<NewBlockSubscription> GetNewBlocks()
        {
            return newBlockSubscription;
        }
    }
}
