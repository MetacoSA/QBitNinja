using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin.Indexer;

namespace RapidBase
{
    public class RapidBaseConfiguration
    {
        public static RapidBaseConfiguration FromConfiguration()
        {
            var conf = new RapidBaseConfiguration {Indexer = IndexerConfiguration.FromConfiguration()};
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
        }

        public CloudTable GetCallbackTable()
        {
            return GetTable("btccallbacks");
        }

        public CloudTable GetTable(string tableName)
        {
            return Indexer.CreateTableClient().GetTableReference(GetFullName(tableName));
        }
        private string GetFullName(string storageObjectName)
        {
            return (Indexer.StorageNamespace + storageObjectName).ToLowerInvariant();
        }

        public CallbackRepository CreateCallbackRepository()
        {
            return new CallbackRepository(this);
        }
    }
}
