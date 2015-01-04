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
            return Indexer.GetTable("btccallbacks");
        }


        public CallbackRepository CreateCallbackRepository()
        {
            return new CallbackRepository(this);
        }
    }
}
