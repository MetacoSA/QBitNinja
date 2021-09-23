using NBitcoin;
using NBitcoin.Indexer.IndexTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QBitNinja
{
    public class CacheBlocksRepository : IBlocksRepository
    {
        private readonly IBlocksRepository _Repo;

        public CacheBlocksRepository(IBlocksRepository repo)
        {
            this._Repo = repo ?? throw new ArgumentNullException("repo");
        }

        List<Tuple<uint256, Block>> _LastAsked = new List<Tuple<uint256, Block>>();  // A cache of blocks.


		#region IBlocksRepository Members

		static readonly int MaxBlocks = 70;  // The maximum number of blocks that may be cached.

        public IEnumerable<NBitcoin.Block> GetBlocks(IEnumerable<NBitcoin.uint256> hashes, CancellationToken cancellationToken)
        {
            var asked = hashes.ToList();

			if (asked.Count > MaxBlocks)  // Asked for more than fit in the cache size, no point in checking cache.
				return _Repo.GetBlocks(hashes, cancellationToken);  // Fetch from repo instead.

            var lastAsked = _LastAsked;

            if (lastAsked != null && asked.SequenceEqual(lastAsked.Select(a => a.Item1)))
                return lastAsked.Select(l=>l.Item2);

            var blocks = _Repo.GetBlocks(hashes, cancellationToken).ToList();

            if (blocks.Count < 5)  // Shouldn't this number match 'MaxBlocks = 70' ?
            {
                _LastAsked = blocks.Select(b => Tuple.Create(b.GetHash(), b)).ToList();
            }
            else
            {
                _LastAsked = null;
            }

            return blocks;
        }
        #endregion
    }
}
