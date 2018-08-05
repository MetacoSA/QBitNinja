using NBitcoin;

#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
    public class NewTransactionNotificationData : NotificationData
    {
        public NewTransactionNotificationData()
        {
            Type = SubscriptionType.NewTransaction;
        }
        public uint256 TransactionId
        {
            get;
            set;
        }
    }
}
