﻿using System.Collections.Generic;
using NBitcoin;
using System.Linq;
using NBitcoin.Crypto;
using NBitcoin.Indexer;
using QBitNinja.Models;
using System;
using System.Text;
using NBitcoin.DataEncoders;
using System.Threading;
using System.Threading.Tasks;

namespace QBitNinja
{
    public class WalletRepository
    {
        public WalletRepository(IndexerClient indexer,
                                Func<Scope, ChainTable<Models.BalanceSummary>> getBalancesCacheTable,
                                CrudTableFactory tableFactory)
        {
            if(indexer == null)
                throw new ArgumentNullException("indexer");
            if(tableFactory == null)
                throw new ArgumentNullException("tableFactory");
            if(getBalancesCacheTable == null)
                throw new ArgumentNullException("getBalancesCacheTable");
            GetBalancesCacheTable = getBalancesCacheTable;
            _walletAddressesTable = tableFactory.GetTable<WalletAddress>("wa");
            _walletTable = tableFactory.GetTable<WalletModel>("wm");
            _keySetTable = tableFactory.GetTable<KeySetData>("ks");
            _keyDataTable = tableFactory.GetTable<HDKeyData>("kd");
            Scope = tableFactory.Scope;
            _indexer = indexer;
        }

        Func<Scope, ChainTable<Models.BalanceSummary>> GetBalancesCacheTable;
        

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

        private bool AddAddress(WalletAddress address)
        {
            if(address.Address == null)
                throw new ArgumentException("Address should not be null", "address.Address");

            return WalletAddressesTable
                .GetChild(address.WalletName)
                .Create(address.Address.ToString(), address, false);
        }

        public List<WalletAddress> Scan(string walletName, KeySetData keysetData, int from, int lookahead)
        {
            List<WalletAddress> addedAddresses = new List<WalletAddress>();
            bool nextUnusedChanged = false;
            int nextToScan = -1;
            while(true)
            {
                List<uint> used = new List<uint>();
                var start = nextToScan == -1 ? from : nextToScan;
                var count = nextToScan == -1 ? lookahead : lookahead - (nextToScan - keysetData.State.NextUnused);
                var addresses = keysetData.KeySet.GetKeys(start)
                                          .Take(count)
                                          .Select(key => WalletAddress.ToWalletAddress(walletName, keysetData, key))
                                          .ToArray();
                nextToScan = from + lookahead;
                HackToPreventOOM(addresses);
                Parallel.ForEach(addresses, address =>
                {
                    bool empty;
                    if(AddWalletAddress(address, true, out empty))
                    {
                        var n = address.HDKey.Path.Indexes.Last();
                        if(!empty)
                        {
                            lock(used)
                                used.Add(n);
                        }
                    }
                });
                addedAddresses.AddRange(addresses);
                if(used.Count == 0)
                    break;

                keysetData.State.NextUnused = (int)(used.Max() + 1);
                nextUnusedChanged = true;
            }
            if(nextUnusedChanged)
                SetKeySet(walletName, keysetData);
            return addedAddresses;
        }
        private static void HackToPreventOOM(WalletAddress[] addresses)
        {
            var addr = addresses.FirstOrDefault();
            if(addr != null)
                addr.CreateWalletRuleEntry().CreateTableEntity();
        }


        private static string Hash(WalletAddress address)
        {
            return Hashes.Hash256(Encoding.UTF8.GetBytes(Serializer.ToString(address))).ToString();
        }

        public WalletAddress[] GetAddresses(string walletName)
        {
            return WalletAddressesTable.GetChild(walletName).Read();
        }

        public bool AddKeySet(string walletName, KeySetData keysetData)
        {
            return KeySetTable.GetChild(walletName).Create(keysetData.KeySet.Name, keysetData, false);
        }
        public bool SetKeySet(string walletName, KeySetData keysetData)
        {
            return KeySetTable.GetChild(walletName).Create(keysetData.KeySet.Name, keysetData, true);
        }

        public bool DeleteKeySet(string walletName, string keyset)
        {
            if(!KeySetTable.GetChild(walletName).Delete(keyset, true))
                return false;

            KeyDataTable.GetChild(walletName, keyset).Delete();
            return true;
        }

        public KeySetData GetKeySetData(string walletName, string keysetName)
        {
            return KeySetTable.GetChild(walletName).ReadOne(keysetName);
        }

        private static string Encode(Script script)
        {
            return Encoders.Hex.EncodeData(script.ToBytes(true));
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

        public bool AddWalletAddress(WalletAddress address, bool mergePast)
        {
            bool merge = false;
            return AddWalletAddress(address, mergePast, out merge);
        }

        public bool AddWalletAddress(WalletAddress address, bool mergePast, out bool empty)
        {
            empty = true;
            if(!AddAddress(address))
                return false;

            if(address.HDKeySet != null)
                KeyDataTable.GetChild(address.WalletName, address.HDKeySet.Name).Create(Encode(address.ScriptPubKey), address.HDKey, false);

            var entry = address.CreateWalletRuleEntry();
            Indexer.AddWalletRule(entry.WalletId, entry.Rule);
            if(mergePast)
            {
                CancellationTokenSource cancel = new CancellationTokenSource();
                cancel.CancelAfter(10000);
                empty = !Indexer.MergeIntoWallet(address.WalletName, address, entry.Rule, cancel.Token);
            }
            if(!empty)
            {
                GetBalanceSummaryCacheTable(address.WalletName, true).Delete();
                GetBalanceSummaryCacheTable(address.WalletName, false).Delete();
            }
            return true;
        }


        public ChainTable<Models.BalanceSummary> GetBalanceSummaryCacheTable(string walletName, bool colored)
        {
            var balanceId = new BalanceId(walletName);
            return GetBalanceSummaryCacheTable(balanceId, colored);
        }

        public ChainTable<Models.BalanceSummary> GetBalanceSummaryCacheTable(BalanceId balanceId, bool colored)
        {
            Scope scope = new Scope(new[] { balanceId.ToString() });
            scope = scope.GetChild(colored ? "colsum" : "balsum");
            var cacheTable = GetBalancesCacheTable(scope);
            return cacheTable;
        }        
    }
}
