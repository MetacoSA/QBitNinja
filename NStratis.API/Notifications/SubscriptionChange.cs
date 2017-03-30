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
        public SubscriptionChange(Subscription subscription, bool added)
        {
            Subscription = subscription;
            Added = added;
        }
        public Subscription Subscription
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
