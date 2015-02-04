using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin.Indexer;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace RapidBase
{
    public class RapidBaseConfiguration
    {
        public RapidBaseConfiguration()
        {
            CoinbaseMaturity = 101;
        }
        public static RapidBaseConfiguration FromConfiguration()
        {
            var conf = new RapidBaseConfiguration
            {
                Indexer = IndexerConfiguration.FromConfiguration(), 
                LocalChain = ConfigurationManager.AppSettings["LocalChain"]
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
                GetCrudTable()
            }.Select(t => t.CreateIfNotExistsAsync()).ToArray();

            Task.WaitAll(tasks);
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

        public ChainTable<T> GetChainCacheTable<T>(string purpose)
        {
            return new ChainTable<T>(GetChainCacheCloudTable())
            {
                Scope = purpose
            };
        }

        ///////

        public int CoinbaseMaturity
        {
            get;
            set;
        }
    }
}
