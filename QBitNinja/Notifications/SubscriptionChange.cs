using QBitNinja.Models;

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
