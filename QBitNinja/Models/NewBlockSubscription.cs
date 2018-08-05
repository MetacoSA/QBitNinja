#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
    public class NewBlockSubscription : Subscription
    {
        public NewBlockSubscription()
        {
            Type = SubscriptionType.NewBlock;
        }
    }
}
