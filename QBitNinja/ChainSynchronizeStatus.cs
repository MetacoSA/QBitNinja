using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QBitNinja
{
    public class ChainSynchronizeStatus
    {
        public bool Synchronizing { get; set; } = true;
        public bool ReindexHeaders { get; set; }
        public int? FileCachedHeight { get; set; }
        public int? TableFetchedHeight { get; set; }
    }
}