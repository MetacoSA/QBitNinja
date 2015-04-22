using NBitcoin;
using NBitcoin.Indexer;
using NBitcoin.Indexer.IndexTasks;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Notifications
{
    public class BlocksUpdater : IDisposable
    {
        private readonly QBitNinjaConfiguration _Configuration;
        public QBitNinjaConfiguration Configuration
        {
            get
            {
                return _Configuration;
            }
        }
        public BlocksUpdater(QBitNinjaConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException("configuration");
            _Configuration = configuration;
        }

        List<IDisposable> _Disposables = new List<IDisposable>();

        private ConcurrentChain _Chain;
        private NBitcoin.Indexer.WalletRuleEntryCollection _Wallets;
        public ConcurrentChain Chain
        {
            get
            {
                return _Chain;
            }
        }
        public void Listen(ConcurrentChain chain = null)
        {
            var indexer = Configuration.Indexer.CreateIndexer();
            ListenerTrace.Info("Handshaked");
            if (chain == null)
            {
                chain = new ConcurrentChain(_Configuration.Indexer.Network);
            }
            _Chain = chain;
            ListenerTrace.Info("Fetching headers from " + _Chain.Tip.Height);
            var client = Configuration.Indexer.CreateIndexerClient();
            client.SynchronizeChain(chain);
            ListenerTrace.Info("Headers fetched tip " + _Chain.Tip.Height);

            ListenerTrace.Info("Fetching wallet rules...");
            _Wallets = _Configuration.Indexer.CreateIndexerClient().GetAllWalletRules();
            ListenerTrace.Info("Wallet rules fetched");

            _Disposables.Add(_Configuration
                .Topics
                .NeedIndexNewBlock
                .OnMessage(header =>
                {
                    client.SynchronizeChain(_Chain);
                    var repo = new IndexerBlocksRepository(client);

                    Task.WaitAll(new[]{
                    Async(() =>
                    {
                        lock (_Wallets)
                        {
                            new IndexBalanceTask(Configuration.Indexer, _Wallets)
                                {
                                    EnsureIsSetup = false
                                }
                                .Index(new BlockFetcher(indexer.GetCheckpoint(IndexerCheckpoints.Wallets), repo, _Chain));
                        }
                    }),
                    Async(() =>
                        {
                            new IndexBalanceTask(Configuration.Indexer, null)
                                {
                                    EnsureIsSetup = false
                                }
                                .Index(new BlockFetcher(indexer.GetCheckpoint(IndexerCheckpoints.Balances), repo, _Chain));
                        }),
                    Async(() =>
                        {
                            new IndexTransactionsTask(Configuration.Indexer)
                                {
                                    EnsureIsSetup = false
                                }
                                .Index(new BlockFetcher(indexer.GetCheckpoint(IndexerCheckpoints.Transactions), repo, _Chain));
                        })});

                    var unused = _Configuration.Topics.NewBlocks.AddAsync(header);
                }));

            _Disposables.Add(Configuration
               .Topics
               .AddedAddresses
               .CreateConsumer()
               .EnsureSubscriptionExists()
               .OnMessage(evt =>
               {
                   ListenerTrace.Info("New wallet rule");
                   lock (_Wallets)
                   {
                       _Wallets.Add(evt.CreateWalletRuleEntry());
                   }
               }));

            _Disposables.Add(Configuration
                .Topics
                .NeedIndexNewTransaction
                .OnMessage(tx =>
                {
                    Task.WaitAll(new[]{
                        Async(() => indexer.IndexOrderedBalance(tx)),
                        Async(()=>{lock (_Wallets)
                        {
                            var balances =
                                OrderedBalanceChange
                                .ExtractWalletBalances(null, tx, null, null, int.MaxValue, _Wallets)
                                .AsEnumerable();
                            indexer.Index(balances);
                        }})

                    });
                    var unused = _Configuration.Topics.NewTransactions.AddAsync(tx);
                }));
        }

        Task Async(Action act)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    act();
                }
                catch (Exception ex)
                {
                    LastException = ex;
                    throw;
                }
            });
        }

        public Exception LastException
        {
            get;
            set;
        }


        #region IDisposable Members

        public void Dispose()
        {
            foreach (var dispo in _Disposables)
                dispo.Dispose();
        }

        #endregion
    }
}
