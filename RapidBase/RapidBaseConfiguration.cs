using NBitcoin.Indexer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase
{
    public class RapidBaseConfiguration
    {
        public static RapidBaseConfiguration FromConfiguration()
        {
            var conf = new RapidBaseConfiguration();
            conf.Indexer = IndexerConfiguration.FromConfiguration();
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
        }
    }
}
