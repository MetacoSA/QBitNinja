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
            _KeySetTable = tableFactory.GetTable<KeySetData>("ks");
            _KeyDataTable = tableFactory.GetTable<HDKeyData>("kd");
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

        private readonly CrudTable<HDKeyData> _KeyDataTable;
        public CrudTable<HDKeyData> KeyDataTable
        {
            get
            {
                return _KeyDataTable;
            }
        }

        private readonly CrudTable<KeySetData> _KeySetTable;
        public CrudTable<KeySetData> KeySetTable
        {
            get
            {
                return _KeySetTable;
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

        public void Create(WalletModel wallet)
        {
            WalletTable.Create(wallet.Name, wallet);
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
                ScriptPubKey = address.Address.ScriptPubKey,
                RedeemScript = address.RedeemScript
            };
            Indexer.AddWalletRule(walletName, rule);
            WalletAddressesTable.GetChild(walletName).Create(Hash(address), address);
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

        public void AddKeySet(string walletName, HDKeySet keyset)
        {
            KeySetData data = new KeySetData();
            data.KeySet = keyset;
            data.State = new HDKeyState();
            KeySetTable.GetChild(walletName).Create(keyset.Name, data);
        }

        public HDKeyData NewKey(string walletName, string keysetName)
        {
            var keySetData = KeySetTable.GetChild(walletName).ReadOne(keysetName);
            KeyPath next = null;
            if (keySetData.State.CurrentPath == null)
            {
                var root = keySetData.KeySet.Path ?? new KeyPath();
                next = root.Derive(0);
            }
            else
            {
                next = Inc(keySetData.State.CurrentPath);
            }
            HDKeyData keyData = new HDKeyData();
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

            KeyDataTable.GetChild(walletName, keysetName).Create(Encode(keyData.ScriptPubKey), keyData);

            keySetData.State.CurrentPath = next;
            KeySetTable.GetChild(walletName).Create(keysetName, keySetData);
            var entry = Indexer.AddWalletRule(walletName, new ScriptRule()
            {
                RedeemScript = keyData.RedeemScript,
                ScriptPubKey = keyData.ScriptPubKey
            });
            var clientIndexer = Indexer.Configuration.CreateIndexerClient();
            clientIndexer.MergeIntoWallet(walletName, keyData.ScriptPubKey, entry.Rule);
            return keyData;
        }

        private string Encode(Script script)
        {
            return Encoders.Hex.EncodeData(script.ToBytes(true));
        }

        private Script CreateScriptPubKey(BitcoinExtPubKey[] bitcoinExtPubKey, int sigCount, bool p2sh)
        {
            if (bitcoinExtPubKey.Length == 1)
            {
                if (p2sh)
                    return bitcoinExtPubKey[0].ExtPubKey.PubKey.ScriptPubKey;
                return bitcoinExtPubKey[0].ExtPubKey.PubKey.Hash.ScriptPubKey;
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

        private KeyPath Inc(KeyPath keyPath)
        {
            var indexes = keyPath.Indexes.ToArray();
            indexes[indexes.Length - 1] = indexes[indexes.Length - 1] + 1;
            return new KeyPath(indexes);
        }
    }
}
