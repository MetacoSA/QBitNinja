using NBitcoin;

#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
    public class NewBlockNotificationData : NotificationData
    {
        public NewBlockNotificationData()
        {
            Type = SubscriptionType.NewBlock;
        }
        public BlockHeader Header
        {
            get;
            set;
        }

        public uint256 BlockId
        {
            get;
            set;
        }

        public int Height
        {
            get;
            set;
        }
    }
}
