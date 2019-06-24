using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NBitcoin;
using NBitcoin.Indexer.IndexTasks;

namespace QBitNinja
{
    public class CacheBlocksRepository : IBlocksRepository
    {
        private const int MaxBlocks = 70;
        private readonly IBlocksRepository _Repo;
        private List<Tuple<uint256, Block>> _LastAsked = new List<Tuple<uint256, Block>>();

        public CacheBlocksRepository(IBlocksRepository repo)
        {
            _Repo = repo ?? throw new ArgumentNullException("repo");
        }

        #region IBlocksRepository Members

        public IEnumerable<Block> GetBlocks(IEnumerable<uint256> hashes, CancellationToken cancellation)
        {
            List<uint256> askedHashes = hashes.ToList();
            if (askedHashes.Count > MaxBlocks)
            {
                return _Repo.GetBlocks(askedHashes, cancellation);
            }

            List<Tuple<uint256, Block>> lastAsked = _LastAsked;
            if (lastAsked != null
             && askedHashes.SequenceEqual(lastAsked.Select(a => a.Item1)))
            {
                return lastAsked.Select(l => l.Item2);
            }

            List<Block> blocks = _Repo.GetBlocks(askedHashes, cancellation).ToList();
            _LastAsked = blocks.Count < 5
                ? blocks.Select(b => Tuple.Create(b.GetHash(), b)).ToList()
                : null;

            return blocks;
        }

        #endregion
    }
}
