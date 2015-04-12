using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin.Indexer;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace QBitNinja
{
    public class QBitNinjaConfiguration
    {
        public QBitNinjaConfiguration()
        {
            CoinbaseMaturity = 101;
        }
        public static QBitNinjaConfiguration FromConfiguration()
        {
            var conf = new QBitNinjaConfiguration
            {
                Indexer = IndexerConfiguration.FromConfiguration(),
                LocalChain = ConfigurationManager.AppSettings["LocalChain"],
                ServiceBus = ConfigurationManager.AppSettings["ServiceBus"]
            };
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
            Indexer.EnsureSetup();

            var tasks = new[]
            {
                GetCallbackTable(),
                GetChainCacheCloudTable(),
                GetCrudTable(),
            }.Select(t => t.CreateIfNotExistsAsync()).ToArray();

            var tasks2 = new Task[]
            { 
                GetWalletRuleListenable().EnsureSetupAsync(),
                GetBroadcastedTransactionsListenable().EnsureSetupAsync(),
            };
            Task.WaitAll(tasks.Concat(tasks2).ToArray());
        }

        public ListenableCloudTable GetBroadcastedTransactionsListenable()
        {
            return new ListenableCloudTable(Indexer.GetTable("broadcastedtx"), ServiceBus, Indexer.GetTable("broadcastedtx").Name);
        }

        public ListenableCloudTable GetWalletRuleListenable()
        {
            return new ListenableCloudTable(null, ServiceBus, Indexer.GetTable("walletrules").Name);
        }

        public CloudTable GetCallbackTable()
        {
            return Indexer.GetTable("rapidcallbacks");
        }

        private CloudTable GetCrudTable()
        {
            return Indexer.GetTable("crudtable");
        }

        private CloudTable GetChainCacheCloudTable()
        {
            return Indexer.GetTable("rapidchainchache");
        }


        //////TODO: These methods will need to be in a "RapidUserConfiguration" that need to know about the user for data isolation (using CrudTable.Scope)
        public CallbackRepository CreateCallbackRepository()
        {
            return new CallbackRepository(new CrudTable<CallbackRegistration>(GetCallbackTable()));
        }

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
