using NBitcoin.Indexer.IndexTasks;
using System.Collections.Generic;
using NBitcoin.Indexer;
using System.Threading;

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

        public IEnumerable<NBitcoin.Block> GetBlocks(IEnumerable<NBitcoin.uint256> hashes, CancellationToken cancellation)
        {
            foreach (var h in hashes)
            {
                yield return _Client.GetBlock(h);
            }
        }

        #endregion
    }
}
