using System.Collections.Generic;
using NBitcoin;
using System.Linq;
using NBitcoin.Crypto;
using NBitcoin.Indexer;
using QBitNinja.Models;
using System;
using System.Text;
using NBitcoin.DataEncoders;
using Newtonsoft.Json.Linq;
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

        readonly Func<Scope, ChainTable<Models.BalanceSummary>> GetBalancesCacheTable;
        
        public Scope Scope
        {
            get;
            set;
        }

        private readonly CrudTable<WalletAddress> _walletAddressesTable;
        public CrudTable<WalletAddress> WalletAddressesTable =>_walletAddressesTable;

        private readonly CrudTable<HDKeyData> _keyDataTable;
        public CrudTable<HDKeyData> KeyDataTable =>_keyDataTable;

        private readonly CrudTable<KeySetData> _keySetTable;
        public CrudTable<KeySetData> KeySetTable => _keySetTable;

        private readonly CrudTable<WalletModel> _walletTable;
        public CrudTable<WalletModel> WalletTable =>_walletTable;

        private readonly IndexerClient _indexer;
        public IndexerClient Indexer => _indexer;

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

        public async Task<List<WalletAddress>> Scan(string walletName, KeySetData keysetData, int from, int lookahead)
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
                HackToPreventOOM(addresses, Network);
                foreach (var address in addresses)
                {
                    var addWalletResult = await AddWalletAddress(address, true);
                    if (addWalletResult.Empty)
                    {
                        var n = address.HDKey.Path.Indexes.Last();
                        if(!addWalletResult.Empty)
                        {
                            lock(used)
                                used.Add(n);
                        }
                    }
                }
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

        private static void HackToPreventOOM(WalletAddress[] addresses, Network network)
        {
            var addr = addresses.FirstOrDefault();
            if(addr != null)
                addr.CreateWalletRuleEntry(network).CreateTableEntity();
        }


        private static string Hash(WalletAddress address, Network network)
        {
            return Hashes.Hash256(Encoding.UTF8.GetBytes(Serializer.ToString(address, network))).ToString();
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

        public Network Network => Indexer.Configuration.Network;

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

        public async Task<(bool Added, bool Empty)> AddWalletAddress(WalletAddress address, bool mergePast)
        {
            var empty = true;
            if(!AddAddress(address))
                return (false, empty);

            if(address.HDKeySet != null)
                KeyDataTable.GetChild(address.WalletName, address.HDKeySet.Name).Create(Encode(address.ScriptPubKey), address.HDKey, false);

            var entry = address.CreateWalletRuleEntry(Network);
            Indexer.AddWalletRule(entry.WalletId, entry.Rule);
            if(mergePast)
            {
                CancellationTokenSource cancel = new CancellationTokenSource();
                cancel.CancelAfter(10000);
                empty = !await Indexer.MergeIntoWallet(address.WalletName, address, entry.Rule, cancel.Token);
            }
            if(!empty)
            {
                GetBalanceSummaryCacheTable(address.WalletName, true).Delete();
                GetBalanceSummaryCacheTable(address.WalletName, false).Delete();
            }
            return (true, empty);
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
