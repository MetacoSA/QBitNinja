#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
    public class NewTransactionSubscription : Subscription
    {
        public NewTransactionSubscription()
        {
            Type = SubscriptionType.NewTransaction;
        }
    }
}
