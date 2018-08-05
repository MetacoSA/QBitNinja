﻿using NBitcoin;
using NBitcoin.Indexer.IndexTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace QBitNinja
{
    public class CacheBlocksRepository : IBlocksRepository
    {
        private IBlocksRepository _Repo;

        public CacheBlocksRepository(IBlocksRepository repo)
        {
            if(repo == null)
                throw new ArgumentNullException("repo");
            this._Repo = repo;
        }

        List<Tuple<uint256, Block>> _LastAsked = new List<Tuple<uint256, Block>>();


		#region IBlocksRepository Members

		int MaxBlocks = 70;

        public IEnumerable<NBitcoin.Block> GetBlocks(IEnumerable<NBitcoin.uint256> hashes, CancellationToken cancellation)
        {
            var asked = hashes.ToList();
			if(asked.Count > MaxBlocks)
				return _Repo.GetBlocks(hashes, cancellation);
            var lastAsked = _LastAsked;

            if(lastAsked != null && asked.SequenceEqual(lastAsked.Select(a => a.Item1)))
                return lastAsked.Select(l=>l.Item2);
            var blocks = _Repo.GetBlocks(hashes, cancellation).ToList();
            if(blocks.Count < 5)
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
