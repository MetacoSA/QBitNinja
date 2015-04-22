using NBitcoin.Indexer.IndexTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Indexer;

namespace QBitNinja.Notifications
{
    public class IndexerBlocksRepository : IBlocksRepository
    {
        IndexerClient _Client;
        public IndexerBlocksRepository(IndexerClient client)
        {
            _Client = client;
        }
        #region IBlocksRepository Members

        public IEnumerable<NBitcoin.Block> GetBlocks(IEnumerable<NBitcoin.uint256> hashes)
        {
            foreach (var h in hashes)
            {
                yield return _Client.GetBlock(h);
            }
        }

        #endregion
    }
}
