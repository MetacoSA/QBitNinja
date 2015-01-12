using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin.Indexer;
using RapidBase.Models;

namespace RapidBase
{
    public class RapidBaseConfiguration
    {
        public RapidBaseConfiguration()
        {
            CoinbaseMaturity = 100;
        }
        public static RapidBaseConfiguration FromConfiguration()
        {
            var conf = new RapidBaseConfiguration
            {
                Indexer = IndexerConfiguration.FromConfiguration()
            };
            return conf;
        }

        public IndexerConfiguration Indexer
        {
            get;
            set;
        }

        public void EnsureSetup()
        {
            Indexer.EnsureSetup();
            GetCallbackTable().CreateIfNotExists();
            GetRapidWalletTable().CreateIfNotExists();
            GetCacheCloudTable().CreateIfNotExists();
            GetChainCacheCloudTable().CreateIfNotExists();
        }

        public CloudTable GetCallbackTable()
        {
            return Indexer.GetTable("rapidcallbacks");
        }

        private CloudTable GetRapidWalletTable()
        {
            return Indexer.GetTable("rapidwallets");
        }
        public CloudTable GetRapidWalletAddressTable()
        {
            return Indexer.GetTable("rapidwalletaddresses");
        }
        private CloudTable GetCacheCloudTable()
        {
            return Indexer.GetTable("rapidcache");
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

        public CrudTable<T> GetCacheTable<T>()
        {
            return new CrudTable<T>(GetCacheCloudTable());
        }

        public WalletRepository CreateWalletRepository()
        {
            return new WalletRepository(
                    Indexer.CreateIndexerClient(),
                    new CrudTable<WalletModel>(GetRapidWalletTable()),
                    new CrudTable<WalletAddress>(GetRapidWalletAddressTable()));
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
