using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RapidBase.Models
{
    public class InsertWalletAddress
    {
        public bool MergePast
        {
            get;
            set;
        }
        public WalletAddress Address
        {
            get;
            set;
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
        public JToken UserData
        {
            get;
            set;
        }

        [JsonIgnore]
        public KeySetData KeysetData
        {
            get
            {
                if (AdditionalInformation == null)
                    return null;
                var prop = AdditionalInformation.Property("keysetData");
                if (prop == null)
                    return null;
                return Serializer.ToObject<KeySetData>(prop.Value.ToString());
            }
        }

        public JObject AdditionalInformation
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
