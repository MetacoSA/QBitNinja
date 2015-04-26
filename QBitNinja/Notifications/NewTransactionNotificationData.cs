using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Notifications
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
