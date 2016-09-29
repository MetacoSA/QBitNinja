using NBitcoin;
using NBitcoin.Indexer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QBitNinja.Tests
{
    public class ChainBuilder
    {
        private readonly ServerTester _parent;

        public ChainBuilder(ServerTester serverTester)
        {
            _parent = serverTester;
            Chain = new ConcurrentChain(Network.TestNet);
        }

        public bool SkipIndexer
        {
            get;
            set;
        }

        public void Broadcast(Transaction funding)
        {
            _ongoingTransactions.Add(funding);
            var indexer = CreateIndexer();
            if (!SkipIndexer)
            {
                indexer.Index(new TransactionEntry.Entity(null, funding, null));
                foreach (var entity in OrderedBalanceChange.ExtractScriptBalances(null, funding, null, null, 0))
                {
                    indexer.Index(new[] { entity });
                }
            }
            if (NewTransaction != null)
                NewTransaction(funding);
        }

        public event Action<Transaction> NewTransaction;
        public event Action<Block> NewBlock;

        private AzureIndexer CreateIndexer()
        {
            var indexer = _parent.Configuration.Indexer.CreateIndexer();
            return indexer;
        }

        readonly List<Transaction> _ongoingTransactions = new List<Transaction>();

        public Transaction EmitMoney(Money money, Script destination, bool broadcast = true, bool coinbase = false)
        {
            var funding = new Transaction()
            {
                Outputs =
                {
                    new TxOut(money, destination),
                    //CreateRandom()
                }
            };
            if (coinbase)
            {
                funding.Inputs.Add(new TxIn()
                {
                    ScriptSig = new Script(RandomUtils.GetBytes(32))
                });
            }
            if (broadcast)
            {
                if (!coinbase)
                    Broadcast(funding);
                else
                    _ongoingTransactions.Add(funding);
            }

            return funding;
        }
        public Transaction EmitMoney(Money money, IDestination destination, bool broadcast = true, bool coinbase = false)
        {
            return EmitMoney(money, destination.ScriptPubKey, broadcast, coinbase);
        }

        private TxOut CreateRandom()
        {
            return new TxOut(Money.Parse("1.2345"), new Key().PubKey);
        }

        internal ChainedBlock AddToChain()
        {
            var header = new BlockHeader();
            header.HashPrevBlock = Chain.Tip.HashBlock;
            header.Nonce = RandomUtils.GetUInt32();
            var prev = Chain.GetBlock(header.HashPrevBlock);
            Chain.SetTip(new ChainedBlock(header, header.GetHash(), prev));
            return Chain.Tip;
        }

        public void ClearMempool()
        {
            _ongoingTransactions.Clear();
        }

        public void DontMine(Transaction tx)
        {
            _ongoingTransactions.Remove(tx);
        }
		internal Block EmitBlock(uint? nonce = null, int blockVersion = 2)
        {
            var block = new Block();
            block.Header.Version = blockVersion;
            block.Transactions.AddRange(_ongoingTransactions.ToList());
            block.Header.HashPrevBlock = Chain.Tip.HashBlock;
            block.Header.Nonce = nonce == null ? RandomUtils.GetUInt32() : nonce.Value;
            if (nonce == null) //if != null, probably want a deterministic block
                block.Header.BlockTime = DateTime.UtcNow;
            var indexer = CreateIndexer();
            var blockHash = block.GetHash();

            var clientIndexer = indexer.Configuration.CreateIndexerClient();
            var height = Chain.Tip.Height + 1;
            if (!SkipIndexer)
            {
                indexer.IndexOrderedBalance(height, block);
                indexer.IndexWalletOrderedBalance(height, block, clientIndexer.GetAllWalletRules());
                indexer.IndexTransactions(height, block);
                if (UploadBlock)
                {
                    indexer.Index(block);
                }
            }
            var prev = Chain.GetBlock(block.Header.HashPrevBlock);
            Chain.SetTip(new ChainedBlock(block.Header, block.GetHash(), prev));
            if (!SkipIndexer)
            {
                indexer.IndexChain(Chain);
                UpdateCheckpoints();
            }
            _ongoingTransactions.Clear();

            if (NewBlock != null)
                NewBlock(block);


            return block;
        }

        private void UpdateCheckpoints()
        {
            var indexer = CreateIndexer();
            UpdateCheckpoint(indexer.GetCheckpoint(IndexerCheckpoints.Balances));
            UpdateCheckpoint(indexer.GetCheckpoint(IndexerCheckpoints.Wallets));
        }

        private void UpdateCheckpoint(Checkpoint checkpoint)
        {
            checkpoint.SaveProgress(Chain.Tip);
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

        public Transaction SendMoney(BitcoinSecret from, BitcoinSecret to, Transaction tx, Money amount)
        {
            var result =
                new TransactionBuilder()
                .AddKeys(from)
                .AddCoins(tx.Outputs.Select(o => new Coin(tx, o)).ToArray())
                .Send(to, amount)
                .SetChange(from.GetAddress())
                .BuildTransaction(true);
            Broadcast(result);
            return result;
        }

        public void SetTip(BlockHeader blockHeader)
        {
            var t = Chain.GetBlock(blockHeader.GetHash());
            Chain.SetTip(t);
        }

    }
}
