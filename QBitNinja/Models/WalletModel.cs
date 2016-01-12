using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#if CLIENT
using QBitNinja.Client.JsonConverters;
using QBitNinja.Client.Models;
#else
using QBitNinja.JsonConverters;
using QBitNinja.Models;
#endif

#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
    public class InsertWalletAddress
    {
        public bool MergePast
        {
            get;
            set;
        }
        public Base58Data Address
        {
            get;
            set;
        }
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
#if !CLIENT
        public static WalletAddress TryParse(string str)
        {
            if(string.IsNullOrEmpty(str))
                return null;
            try
            {
                return Serializer.ToObject<WalletAddress>(str);
            }
            catch
            {
                return null;
            }
        }
#endif

        public string WalletName
        {
            get;
            set;
        }

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


        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public HDKeySet HDKeySet
        {
            get;
            set;
        }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public HDKeyData HDKey
        {
            get;
            set;
        }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public JToken UserData
        {
            get;
            set;
        }

        public static WalletAddress ToWalletAddress(string walletName, KeySetData keysetData, HDKeyData key)
        {
            WalletAddress address = new WalletAddress();
            address.WalletName = walletName;
            address.RedeemScript = key.RedeemScript;
            address.Address = key.Address;
            address.HDKey = key;
            address.HDKeySet = keysetData.KeySet;
            return address;
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
