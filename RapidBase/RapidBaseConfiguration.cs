using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin.Indexer;
using RapidBase.Models;

namespace RapidBase
{
    public class RapidBaseConfiguration
    {
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

        //////TODO: These methods will need to be in a "RapidUserConfiguration" that need to know about the user for data isolation (using CrudTable.Scope)
        public CallbackRepository CreateCallbackRepository()
        {
            return new CallbackRepository(new CrudTable<CallbackRegistration>(this.GetCallbackTable()));
        }

        public WalletRepository CreateWalletRepository()
        {
            return new WalletRepository(
                    Indexer.CreateIndexerClient(), 
                    new CrudTable<WalletModel>(this.GetRapidWalletTable()),
                    new CrudTable<WalletAddress>(this.GetRapidWalletAddressTable()));
        }
        ///////
    }
}
