using System.Collections.Generic;
using NBitcoin;
using System.Linq;
using NBitcoin.Crypto;
using NBitcoin.Indexer;
using RapidBase.Models;
using System;
using System.Text;
using NBitcoin.DataEncoders;

namespace RapidBase
{
    public class WalletRepository
    {
        public WalletRepository(IndexerClient indexer,
                                CrudTableFactory tableFactory)
        {
            if (indexer == null)
                throw new ArgumentNullException("indexer");
            if (tableFactory == null)
                throw new ArgumentNullException("tableFactory");
            _walletAddressesTable = tableFactory.GetTable<WalletAddress>("wa");
            _walletTable = tableFactory.GetTable<WalletModel>("wm");
            _keySetTable = tableFactory.GetTable<KeySetData>("ks");
            _keyDataTable = tableFactory.GetTable<HDKeyData>("kd");
            Scope = tableFactory.Scope;
            _indexer = indexer;
        }

        public Scope Scope
        {
            get;
            set;
        }

        private readonly CrudTable<WalletAddress> _walletAddressesTable;
        public CrudTable<WalletAddress> WalletAddressesTable
        {
            get
            {
                return _walletAddressesTable;
            }
        }

        private readonly CrudTable<HDKeyData> _keyDataTable;
        public CrudTable<HDKeyData> KeyDataTable
        {
            get
            {
                return _keyDataTable;
            }
        }

        private readonly CrudTable<KeySetData> _keySetTable;
        public CrudTable<KeySetData> KeySetTable
        {
            get
            {
                return _keySetTable;
            }
        }

        private readonly CrudTable<WalletModel> _walletTable;
        public CrudTable<WalletModel> WalletTable
        {
            get
            {
                return _walletTable;
            }
        }

        private readonly IndexerClient _indexer;
        public IndexerClient Indexer
        {
            get
            {
                return _indexer;
            }
        }

        public bool Create(WalletModel wallet)
        {
            return WalletTable.Create(wallet.Name, wallet, false);
        }

        public WalletModel GetWallet(string walletName)
        {
            return WalletTable.ReadOne(walletName);
        }

        public WalletModel[] Get()
        {
            return WalletTable.Read();
        }

        public ScriptRule AddAddress(string walletName, WalletAddress address)
        {
            if (address.Address == null)
                throw new ArgumentException("Address should not be null", "address.Address");
            var rule = new ScriptRule
            {
                CustomData = address.CustomData == null ? null : address.CustomData.ToString(),
                ScriptPubKey = address.ScriptPubKey,
                RedeemScript = address.RedeemScript
            };
            if (!WalletAddressesTable.GetChild(walletName).Create(address.Address.ToString(), address, false))
                return null;
            Indexer.AddWalletRule(walletName, rule);
            return rule;
        }

        private static string Hash(WalletAddress address)
        {
            return Hashes.Hash256(Encoding.UTF8.GetBytes(Serializer.ToString(address))).ToString();
        }

        public WalletAddress[] GetAddresses(string walletName)
        {
            return WalletAddressesTable.GetChild(walletName).Read();
        }

        public bool AddKeySet(string walletName, HDKeySet keyset)
        {
            KeySetData data = new KeySetData
            {
                KeySet = keyset,
                State = new HDKeyState()
            };
            return KeySetTable.GetChild(walletName).Create(keyset.Name, data, false);
        }

        public bool DeleteKeySet(string walletName, string keyset)
        {
            if (!KeySetTable.GetChild(walletName).Delete(keyset, true))
                return false;

            KeyDataTable.GetChild(walletName, keyset).Delete();
            return true;
        }

        public HDKeyData NewKey(string walletName, string keysetName)
        {
            HDKeyData keyData;
            while (true)
            {
                var keySetData = GetKeySetData(walletName, keysetName);
                if (keySetData == null)
                    return null;
                KeyPath next;
                if (keySetData.State.CurrentPath == null)
                {
                    var root = keySetData.KeySet.Path ?? new KeyPath();
                    next = root.Derive(0);
                }
                else
                {
                    next = Inc(keySetData.State.CurrentPath);
                }
                keyData = new HDKeyData();
                keyData.ExtPubKeys = keySetData
                                      .KeySet
                                      .ExtPubKeys
                                      .Select(k => k.ExtPubKey.Derive(next).GetWif(Network)).ToArray();
                keyData.Path = next;
                keyData.RedeemScript = CreateScriptPubKey(keyData.ExtPubKeys, keySetData.KeySet.SignatureCount, !keySetData.KeySet.NoP2SH);
                if (keySetData.KeySet.NoP2SH)
                {
                    keyData.ScriptPubKey = keyData.RedeemScript;
                    keyData.RedeemScript = null;
                    keyData.Address = keyData.ScriptPubKey.GetDestinationAddress(Network);
                }
                else
                {
                    keyData.ScriptPubKey = keyData.RedeemScript.Hash.ScriptPubKey;
                    keyData.Address = keyData.ScriptPubKey.GetDestinationAddress(Network);
                }

                if (KeyDataTable.GetChild(walletName, keysetName).Create(Encode(keyData.ScriptPubKey), keyData, false))
                {
                    keySetData.State.CurrentPath = next;
                    KeySetTable.GetChild(walletName).Create(keysetName, keySetData);
                    break;
                }
            }
            var entry = Indexer.AddWalletRule(walletName, new ScriptRule
            {
                RedeemScript = keyData.RedeemScript,
                ScriptPubKey = keyData.ScriptPubKey
            });
            var clientIndexer = Indexer.Configuration.CreateIndexerClient();
            clientIndexer.MergeIntoWallet(walletName, keyData.ScriptPubKey, entry.Rule);
            return keyData;
        }

        public KeySetData GetKeySetData(string walletName, string keysetName)
        {
            return KeySetTable.GetChild(walletName).ReadOne(keysetName);
        }

        private static string Encode(Script script)
        {
            return Encoders.Hex.EncodeData(script.ToBytes(true));
        }

        private static Script CreateScriptPubKey(IList<BitcoinExtPubKey> bitcoinExtPubKey, int sigCount, bool p2sh)
        {
            if (bitcoinExtPubKey.Count == 1)
            {
                return p2sh ? bitcoinExtPubKey[0].ExtPubKey.PubKey.ScriptPubKey : bitcoinExtPubKey[0].ExtPubKey.PubKey.Hash.ScriptPubKey;
            }
            return PayToMultiSigTemplate.Instance.GenerateScriptPubKey(sigCount, bitcoinExtPubKey.Select(k => k.ExtPubKey.PubKey).ToArray());
        }

        public Network Network
        {
            get
            {
                return Indexer.Configuration.Network;
            }
        }

        private static KeyPath Inc(KeyPath keyPath)
        {
            var indexes = keyPath.Indexes.ToArray();
            indexes[indexes.Length - 1] = indexes[indexes.Length - 1] + 1;
            return new KeyPath(indexes);
        }

        public KeySetData[] GetKeysets(string walletName)
        {
            return KeySetTable.GetChild(walletName).Read();
        }

        internal HDKeyData[] GetKeys(string walletName, string keysetName)
        {
            return KeyDataTable.GetChild(walletName, keysetName).Read();
        }
    }
}
