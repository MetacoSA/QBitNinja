using NBitcoin;
using NBitcoin.Indexer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase.Tests
{
    public class ChainBuilder
    {
        private ServerTester _Parent;

        public ChainBuilder(ServerTester serverTester)
        {
            this._Parent = serverTester;
            Chain = new ConcurrentChain(Network.TestNet);
        }

        public void Broadcast(Transaction funding)
        {
            _OngoingTransactions.Add(funding);
            var indexer = CreateIndexer();
            indexer.Index(new TransactionEntry.Entity(null, funding, null));
            foreach (var entity in OrderedBalanceChange.ExtractScriptBalances(null, funding, null, null, 0))
            {
                indexer.Index(new[] { entity });
            }
        }

        private AzureIndexer CreateIndexer()
        {
            var indexer = _Parent.Configuration.Indexer.CreateIndexer();
            return indexer;
        }

        List<Transaction> _OngoingTransactions = new List<Transaction>();

        public Transaction EmitMoney(Money money, IDestination destination)
        {
            var funding = new Transaction()
            {
                Outputs =
                {
                    new TxOut(money, destination),
                    //CreateRandom()
                }
            };
            Broadcast(funding);
            return funding;
        }

        private TxOut CreateRandom()
        {
            return new TxOut(Money.Parse("1.2345"), new Key().PubKey);
        }

        internal Block EmitBlock()
        {
            var block = new Block();
            block.Transactions.AddRange(_OngoingTransactions.ToList());
            block.Header.HashPrevBlock = Chain.Tip.HashBlock;
            block.Header.Nonce = RandomUtils.GetUInt32();
            var indexer = CreateIndexer();
            var blockHash = block.GetHash();
            foreach (var tx in block.Transactions)
            {
                indexer.Index(new TransactionEntry.Entity(null, tx, blockHash));
                foreach (var entity in OrderedBalanceChange.ExtractScriptBalances(null, tx, blockHash, block.Header, Chain.Tip.Height + 1))
                {
                    indexer.Index(new[] { entity });
                }
            }
            if (UploadBlock)
            {
                indexer.Index(block);
            }
            var prev = Chain.GetBlock(block.Header.HashPrevBlock);
            Chain.SetTip(new ChainedBlock(block.Header, block.GetHash(), prev));
            indexer.IndexChain(Chain);
            _OngoingTransactions.Clear();
            return block;
        }

        public ChainBase Chain
        {
            get;
            set;
        }

        public bool UploadBlock
        {
            get;
            set;
        }
    }
}
