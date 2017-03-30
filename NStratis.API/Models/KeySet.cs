using NBitcoin;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;

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
            return KeySet.GetKey(State.NextUnused + lookahead);
        }        

        public IEnumerable<HDKeyData> GetUnuseds(int from = 0)
        {
            for(int i = from; true; i++)
            {
                yield return GetUnused(i);
            }
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
        public HDKeySet()
        {
            P2SH = true;
            LexicographicOrder = true;
        }
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

        public bool LexicographicOrder
        {
            get;
            set;
        }
        public bool P2SH
        {
            get;
            set;
        }

        public HDKeyData GetKey(int n)
        {
            var network = ExtPubKeys.Select(e => e.Network).FirstOrDefault();
            var root = Path ?? new KeyPath();
            var next = root.Derive(n, false);

            var keyData = new HDKeyData();
            keyData.ExtPubKeys = ExtPubKeys
                                  .Select(k => k.ExtPubKey.Derive(next).GetWif(network)).ToArray();
            keyData.Path = next;
            keyData.RedeemScript = CreateScriptPubKey(keyData.ExtPubKeys);
            if(!P2SH)
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

        public IEnumerable<HDKeyData> GetKeys(int from = 0)
        {
            for(int i = from; true; i++)
            {
                yield return GetKey(i);
            }
        }

        private Script CreateScriptPubKey(IList<BitcoinExtPubKey> bitcoinExtPubKey)
        {
            if(bitcoinExtPubKey.Count == 1)
            {
                return P2SH ? bitcoinExtPubKey[0].ExtPubKey.PubKey.ScriptPubKey : bitcoinExtPubKey[0].ExtPubKey.PubKey.Hash.ScriptPubKey;
            }
            var keys = bitcoinExtPubKey.Select(k => k.ExtPubKey.PubKey).ToArray();
            if(LexicographicOrder)
                keys = keys.OrderBy(p => p.ToHex()).ToArray();
            return PayToMultiSigTemplate.Instance.GenerateScriptPubKey(SignatureCount, keys);
        }

    }
}
