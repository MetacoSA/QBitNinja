using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Notifications
{
    public class NewTransactionSubscription : Subscription
    {
        public NewTransactionSubscription()
        {
            Type = SubscriptionType.NewTransaction;
        }
    }
}
