using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase.Models
{
    public class WalletAddress
    {
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public BitcoinAddress Address
        {
            get;
            set;
        }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Script RedeemScript
        {
            get;
            set;
        }
        public JObject CustomData
        {
            get;
            set;
        }
    }
    public class WalletModel
    {
        public string Name
        {
            get;
            set;
        }
    }
}
