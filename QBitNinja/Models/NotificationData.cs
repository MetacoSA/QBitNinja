using Newtonsoft.Json;
#if !CLIENT
using QBitNinja.JsonConverters;
#else
using QBitNinja.Client.JsonConverters;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
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
