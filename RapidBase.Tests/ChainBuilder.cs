using NBitcoin;
using NBitcoin.Indexer;
using System.Collections.Generic;
using System.Linq;

namespace RapidBase.Tests
{
    public class ChainBuilder
    {
        private readonly ServerTester _parent;

        public ChainBuilder(ServerTester serverTester)
        {
            _parent = serverTester;
            Chain = new ConcurrentChain(Network.TestNet);
        }

        public void Broadcast(Transaction funding)
        {
            _ongoingTransactions.Add(funding);
            var indexer = CreateIndexer();
            indexer.Index(new TransactionEntry.Entity(null, funding, null));
            foreach (var entity in OrderedBalanceChange.ExtractScriptBalances(null, funding, null, null, 0))
            {
                indexer.Index(new[] { entity });
            }
        }

        private AzureIndexer CreateIndexer()
        {
            var indexer = _parent.Configuration.Indexer.CreateIndexer();
            return indexer;
        }

        readonly List<Transaction> _ongoingTransactions = new List<Transaction>();

        public Transaction EmitMoney(Money money, IDestination destination, bool broadcast = true)
        {
            var funding = new Transaction()
            {
                Outputs =
                {
                    new TxOut(money, destination),
                    //CreateRandom()
                }
            };
            if (broadcast)
                Broadcast(funding);
            return funding;
        }

        private TxOut CreateRandom()
        {
            return new TxOut(Money.Parse("1.2345"), new Key().PubKey);
        }

        internal Block EmitBlock(uint? nonce = null)
        {
            var block = new Block();
            block.Transactions.AddRange(_ongoingTransactions.ToList());
            block.Header.HashPrevBlock = Chain.Tip.HashBlock;
            block.Header.Nonce = nonce == null ? RandomUtils.GetUInt32() : nonce.Value;
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
            _ongoingTransactions.Clear();
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
