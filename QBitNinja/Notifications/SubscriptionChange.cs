using QBitNinja.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Notifications
{
    public class SubscriptionChange
    {
        public SubscriptionChange()
        {

        }
        public SubscriptionChange(NewBlockSubscription subscription, bool added)
        {
            Subscription = subscription;
            Added = added;
        }
        public NewBlockSubscription Subscription
        {
            get;
            set;
        }
        public bool Added
        {
            get;
            set;
        }
    }
}
