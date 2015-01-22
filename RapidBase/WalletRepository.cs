using NBitcoin;
using System.Linq;
using NBitcoin.Crypto;
using NBitcoin.Indexer;
using RapidBase.Models;
using System;
using System.Text;

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
            _indexer = indexer;
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
            WalletTable.Create("w", wallet.Name.ToLowerInvariant(), wallet);
        }

        public WalletModel[] Get()
        {
            return WalletTable.Read("w");
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
            WalletAddressesTable.Create(walletName.ToLowerInvariant(), Hash(address), address);
            return rule;
        }

        private static string Hash(WalletAddress address)
        {
            return Hashes.Hash256(Encoding.UTF8.GetBytes(Serializer.ToString(address))).ToString();
        }

        public WalletAddress[] GetAddresses(string walletName)
        {
            return WalletAddressesTable.Read(walletName.ToLowerInvariant());
        }

        public void AddKeySet(string walletName, HDKeySet keyset)
        {
            KeySetData data = new KeySetData();
            data.KeySet = keyset;
            data.State = new HDKeyState();
            KeySetTable.Create(walletName, keyset.Name, data);
            KeySetTable.Read(walletName);
            KeySetTable.ReadOne(walletName, keyset.Name);
        }

        public HDKeyData NewKey(string walletName, string keysetName)
        {
            var keySetData = KeySetTable.ReadOne(walletName, keysetName);
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
            keyData.ExtPubKey = keySetData.KeySet.ExtPubKey.ExtPubKey.Derive(next).GetWif(Network);
            keyData.Path = next;
            keyData.Address = keyData.ExtPubKey.ExtPubKey.PubKey.GetAddress(Network);

            KeyDataTable.Create(Concat(walletName, keysetName), keyData.Address.ToString(), keyData);

            keySetData.State.CurrentPath = next;
            KeySetTable.Create(walletName, keysetName, keySetData);
            return keyData;
        }

        private string Concat(string walletName, string keysetName)
        {
            return walletName + "µ" + keysetName;
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
