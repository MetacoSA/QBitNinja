using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer;
using QBitNinja.Models;

namespace QBitNinja
{
    public class WalletRepository
    {
        public WalletRepository(
            IndexerClient indexer,
            Func<Scope, ChainTable<BalanceSummary>> getBalancesCacheTable,
            CrudTableFactory tableFactory)
        {
            Indexer = indexer ?? throw new ArgumentNullException("indexer");
            GetBalancesCacheTable = getBalancesCacheTable ?? throw new ArgumentNullException("getBalancesCacheTable");
            tableFactory = tableFactory ?? throw new ArgumentNullException("tableFactory");

            WalletAddressesTable = tableFactory.GetTable<WalletAddress>("wa");
            WalletTable = tableFactory.GetTable<WalletModel>("wm");
            KeySetTable = tableFactory.GetTable<KeySetData>("ks");
            KeyDataTable = tableFactory.GetTable<HDKeyData>("kd");
            Scope = tableFactory.Scope;
        }

        private Func<Scope, ChainTable<BalanceSummary>> GetBalancesCacheTable;
        
        public Scope Scope { get; set; }

        public CrudTable<WalletAddress> WalletAddressesTable { get; }
        public CrudTable<HDKeyData> KeyDataTable { get; }
        public CrudTable<KeySetData> KeySetTable { get; }
        public CrudTable<WalletModel> WalletTable { get; }
        public IndexerClient Indexer { get; }

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
            if (address.Address == null)
            {
                throw new ArgumentException("Address should not be null", "address.Address");
            }

            return WalletAddressesTable
                .GetChild(address.WalletName)
                .Create(address.Address.ToString(), address, false);
        }

        public List<WalletAddress> Scan(string walletName, KeySetData keysetData, int from, int lookahead)
        {
            var addedAddresses = new List<WalletAddress>();
            var nextUnusedChanged = false;
            int nextToScan = -1;
            while (true)
            {
                List<uint> used = new List<uint>();
                int start = nextToScan == -1 ? from : nextToScan;
                int count = nextToScan == -1 ? lookahead : lookahead - (nextToScan - keysetData.State.NextUnused);
                WalletAddress[] addresses = keysetData.KeySet.GetKeys(start)
                                          .Take(count)
                                          .Select(key => WalletAddress.ToWalletAddress(walletName, keysetData, key))
                                          .ToArray();
                nextToScan = from + lookahead;
                HackToPreventOOM(addresses);
                Parallel.ForEach(addresses, address =>
                {
                    if (AddWalletAddress(address, true, out bool empty))
                    {
                        uint n = address.HDKey.Path.Indexes.Last();
                        if (!empty)
                        {
                            lock (used)
                            {
                                used.Add(n);
                            }
                        }
                    }
                });
                addedAddresses.AddRange(addresses);
                if (used.Count == 0)
                {
                    break;
                }

                keysetData.State.NextUnused = (int)(used.Max() + 1);
                nextUnusedChanged = true;
            }

            if(nextUnusedChanged)
            {
                SetKeySet(walletName, keysetData);
            }

            return addedAddresses;
        }
        private static void HackToPreventOOM(WalletAddress[] addresses)
        {
            WalletAddress addr = addresses.FirstOrDefault();
            addr?.CreateWalletRuleEntry().CreateTableEntity();
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
            if (!KeySetTable.GetChild(walletName).Delete(keyset, true))
            {
                return false;
            }

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

        public Network Network => Indexer.Configuration.Network;

        private static KeyPath Inc(KeyPath keyPath)
        {
            uint[] indexes = keyPath.Indexes.ToArray();
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
            var merge = false;
            return AddWalletAddress(address, mergePast, out merge);
        }

        public bool AddWalletAddress(WalletAddress address, bool mergePast, out bool empty)
        {
            empty = true;
            if (!AddAddress(address))
            {
                return false;
            }

            if (address.HDKeySet != null)
            {
                KeyDataTable
                    .GetChild(address.WalletName, address.HDKeySet.Name)
                    .Create(Encode(address.ScriptPubKey), address.HDKey, false);
            }

            WalletRuleEntry entry = address.CreateWalletRuleEntry();
            Indexer.AddWalletRule(entry.WalletId, entry.Rule);
            if (mergePast)
            {
                CancellationTokenSource cancel = new CancellationTokenSource();
                cancel.CancelAfter(10000);
                empty = !Indexer.MergeIntoWallet(address.WalletName, address, entry.Rule, cancel.Token);
            }

            if (empty)
            {
                return true;
            }

            GetBalanceSummaryCacheTable(address.WalletName, true).Delete();
            GetBalanceSummaryCacheTable(address.WalletName, false).Delete();

            return true;
        }


        public ChainTable<BalanceSummary> GetBalanceSummaryCacheTable(string walletName, bool colored)
        {
            BalanceId balanceId = new BalanceId(walletName);
            return GetBalanceSummaryCacheTable(balanceId, colored);
        }

        public ChainTable<BalanceSummary> GetBalanceSummaryCacheTable(BalanceId balanceId, bool colored)
        {
            Scope scope = new Scope(new[] { balanceId.ToString() });
            scope = scope.GetChild(colored ? "colsum" : "balsum");
            ChainTable<BalanceSummary> cacheTable = GetBalancesCacheTable(scope);
            return cacheTable;
        }        
    }
}
