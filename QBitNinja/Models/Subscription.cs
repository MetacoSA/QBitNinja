using Newtonsoft.Json;
#if !CLIENT
using QBitNinja.JsonConverters;
#else
using QBitNinja.Client.JsonConverters;
using QBitNinja.Client.Models;
#endif

#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{    
    [JsonConverter(typeof(EnumAliasJsonConverter))]
    public enum SubscriptionType
    {
        [EnumAlias("new-block")]
        NewBlock,
        [EnumAlias("new-transaction")]
        NewTransaction
    }

    [JsonConverter(typeof(EnumTypeJsonConverter))]
    [EnumType(SubscriptionType.NewBlock, typeof(NewBlockSubscription))]
    [EnumType(SubscriptionType.NewTransaction, typeof(NewTransactionSubscription))]
    public class Subscription
    {
        public string Id
        {
            get;
            set;
        }
        public string Url
        {
            get;
            set;
        }
        public SubscriptionType Type
        {
            get;
            set;
        }
    }
}
