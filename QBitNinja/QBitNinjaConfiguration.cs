using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.Indexer;
using NBitcoin.Protocol;
using QBitNinja.Models;
using QBitNinja.Notifications;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja
{
    public class QBitNinjaConfiguration
    {
        public QBitNinjaConfiguration()
        {
            CoinbaseMaturity = 100;
        }
        public static QBitNinjaConfiguration FromConfiguration(IConfiguration configuration)
        {
            var conf = new QBitNinjaConfiguration
            {
                Indexer = IndexerConfiguration.FromConfiguration(configuration),
                LocalChain = configuration["LocalChain"],
                ServiceBus = configuration["ServiceBus"]
            };
			conf.CoinbaseMaturity = conf.Indexer.Network.Consensus.CoinbaseMaturity;
            return conf;
        }

        public IndexerConfiguration Indexer
        {
            get;
            set;
        }

        public string LocalChain
        {
            get;
            set;
        }

        public void EnsureSetup()
        {

            var tasks = new[]
            {
                GetChainCacheCloudTable(),
                GetCrudTable(),
                GetRejectTable().Table,
            }.Select(t => t.CreateIfNotExistsAsync()).OfType<Task>().ToList();

            tasks.Add(Indexer.EnsureSetupAsync());

            Task.WaitAll(tasks.ToArray());
        }


        public CrudTable<RejectPayload> GetRejectTable()
        {
            return GetCrudTableFactory().GetTable<RejectPayload>("rejectedbroadcasted");
        }

        private CloudTable GetCrudTable()
        {
            return Indexer.GetTable("crudtable");
        }

        private CloudTable GetChainCacheCloudTable()
        {
            return Indexer.GetTable("chainchache");
        }

		public CrudTable<BlockRange> GetInitialIndexingQueued()
		{
			return GetCrudTableFactory().GetTable<BlockRange>("initialindexing_queued");
		}

		public CrudTable<BlockRange> GetInitialIndexingProcessing()
		{
			return GetCrudTableFactory().GetTable<BlockRange>("initialindexing_processing");
		}

		public CrudTable<BroadcastedTransaction> BroadcastedTransactions()
		{
			return GetCrudTableFactory().GetTable<BroadcastedTransaction>("broadcasted_demands");
		}

		//////TODO: These methods will need to be in a "RapidUserConfiguration" that need to know about the user for data isolation (using CrudTable.Scope)

		public CrudTable<T> GetCacheTable<T>(Scope scope = null)
        {
            return GetCrudTableFactory(scope).GetTable<T>("cache");
        }

        public CrudTableFactory GetCrudTableFactory(Scope scope = null)
        {
            return new CrudTableFactory(GetCrudTable, scope);
        }

        public WalletRepository CreateWalletRepository(Scope scope = null)
        {
            return new WalletRepository(
                    Indexer.CreateIndexerClient(),
                    GetChainCacheTable<BalanceSummary>,
                    GetCrudTableFactory(scope));
        }

        public ChainTable<T> GetChainCacheTable<T>(Scope scope)
        {
            return new ChainTable<T>(GetChainCacheCloudTable())
            {
                Scope = scope
            };
        }

        ///////

        public int CoinbaseMaturity
        {
            get;
            set;
        }

        public string ServiceBus
        {
            get;
            set;
        }
    }
}
