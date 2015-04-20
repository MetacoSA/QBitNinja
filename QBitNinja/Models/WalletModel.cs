using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QBitNinja.Models
{
    public class InsertWalletAddress
    {
        public bool MergePast
        {
            get;
            set;
        }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Base58Data Address
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
        public JToken UserData
        {
            get;
            set;
        }

        public bool IsCoherent()
        {
            BitcoinScriptAddress scriptAddress = Address as BitcoinScriptAddress;
            if (scriptAddress != null && RedeemScript != null)
            {
                return scriptAddress.Hash == RedeemScript.Hash;
            }
            if (scriptAddress == null && RedeemScript != null)
            {
                return false;
            }
            return true;
        }
    }
    public class WalletAddress : IDestination
    {
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Base58Data Address
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

        [JsonIgnore]
        public KeySetData KeysetData
        {
            get
            {
                var info = AdditionalInformation as JObject;
                if (info == null)
                    return null;
                var prop = info.Property("keysetData");
                if (prop == null)
                    return null;
                return Serializer.ToObject<KeySetData>(prop.Value.ToString());
            }
        }

        public JToken AdditionalInformation
        {
            get;
            set;
        }

        
        #region IDestination Members

        [JsonIgnore]
        public Script ScriptPubKey
        {
            get
            {
                var dest = Address as IDestination;
                if (dest == null)
                {
                    if (RedeemScript == null)
                        return null;
                    return RedeemScript.Hash.ScriptPubKey;
                }
                return dest.ScriptPubKey;
            }
        }

        #endregion

        public string WalletName
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
