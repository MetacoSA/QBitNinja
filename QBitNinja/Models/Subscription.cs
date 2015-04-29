using Newtonsoft.Json;
using System.Reflection;
using QBitNinja.JsonConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QBitNinja.Models;
using System.IO;
using Newtonsoft.Json.Linq;

namespace QBitNinja.Models
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
