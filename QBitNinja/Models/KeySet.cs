using NBitcoin;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;

#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
    public class KeySetData
    {
        public HDKeySet KeySet
        {
            get;
            set;
        }
        public HDKeyState State
        {
            get;
            set;
        }

        public HDKeyData GetUnused(int lookahead)
        {
            var network = KeySet.ExtPubKeys.Select(e => e.Network).FirstOrDefault();
            var root = KeySet.Path ?? new KeyPath();
            var next = root.Derive(State.NextUnused + lookahead, false);

            var keyData = new HDKeyData();
            keyData.ExtPubKeys = KeySet
                                  .ExtPubKeys
                                  .Select(k => k.ExtPubKey.Derive(next).GetWif(network)).ToArray();
            keyData.Path = next;
            keyData.RedeemScript = CreateScriptPubKey(keyData.ExtPubKeys, KeySet.SignatureCount, !KeySet.NoP2SH);
            if(KeySet.NoP2SH)
            {
                keyData.ScriptPubKey = keyData.RedeemScript;
                keyData.RedeemScript = null;
                keyData.Address = keyData.ScriptPubKey.GetDestinationAddress(network);
            }
            else
            {
                keyData.ScriptPubKey = keyData.RedeemScript.Hash.ScriptPubKey;
                keyData.Address = keyData.ScriptPubKey.GetDestinationAddress(network);
            }            
            return keyData;
        }

        public IEnumerable<HDKeyData> GetUnuseds(int lookahead = 20)
        {
            for(int i = 0; i < lookahead; i++)
            {
                yield return GetUnused(i);
            }
        }
        private static Script CreateScriptPubKey(IList<BitcoinExtPubKey> bitcoinExtPubKey, int sigCount, bool p2sh)
        {
            if(bitcoinExtPubKey.Count == 1)
            {
                return p2sh ? bitcoinExtPubKey[0].ExtPubKey.PubKey.ScriptPubKey : bitcoinExtPubKey[0].ExtPubKey.PubKey.Hash.ScriptPubKey;
            }
            return PayToMultiSigTemplate.Instance.GenerateScriptPubKey(sigCount, bitcoinExtPubKey.Select(k => k.ExtPubKey.PubKey).ToArray());
        }
    }

    public class HDKeyData
    {
        public KeyPath Path
        {
            get;
            set;
        }
        public BitcoinAddress Address
        {
            get;
            set;
        }
        public BitcoinExtPubKey[] ExtPubKeys
        {
            get;
            set;
        }

        public Script RedeemScript
        {
            get;
            set;
        }

        public Script ScriptPubKey
        {
            get;
            set;
        }
    }

    public class HDKeyState
    {
        public int NextUnused
        {
            get;
            set;
        }
    }
    public class HDKeySet
    {
        public string Name
        {
            get;
            set;
        }
        public BitcoinExtPubKey[] ExtPubKeys
        {
            get;
            set;
        }

        public int SignatureCount
        {
            get;
            set;
        }

        public KeyPath Path
        {
            get;
            set;
        }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool NoP2SH
        {
            get;
            set;
        }

    }
}
