using Newtonsoft.Json;
using QBitNinja.JsonConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Models
{

    [JsonConverter(typeof(EnumTypeJsonConverter))]
    [EnumType(SubscriptionType.NewBlock, typeof(NewBlockNotificationData))]
    [EnumType(SubscriptionType.NewTransaction, typeof(NewTransactionNotificationData))]
    public class NotificationData
    {
        public SubscriptionType Type
        {
            get;
            set;
        }
    }
}
