using Microsoft.ServiceBus.Messaging;
using NBitcoin;
using NBitcoin.Indexer;
using NBitcoin.Indexer.IndexTasks;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
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

        SubscriptionCollection _Subscriptions = null;
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

            ListenerTrace.Info("Fetching wallet subscriptions...");
            _Subscriptions = new SubscriptionCollection(_Configuration.GetSubscriptionsTable().Read());
            ListenerTrace.Info("Subscriptions fetched");


            _Disposables.Add(_Configuration
                .Topics
                .SendNotifications
                .OnMessageAsync((n, act) =>
                {
                    return SendAsync(n, act).ContinueWith(ErrorHandler);
                }, new OnMessageOptions()
                {
                    MaxConcurrentCalls = 1000,
                    AutoComplete = true,
                }));

            _Disposables.Add(_Configuration
                .Topics
                .NeedIndexNewBlock
                .OnMessageAsync(async header =>
                {
                    client.SynchronizeChain(_Chain);
                    var repo = new IndexerBlocksRepository(client);

                    await Task.WhenAll(new[]{
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
                        }),
                     Async(() =>
                        {
                            new IndexNotificationsTask(Configuration, _Subscriptions)
                                {
                                    EnsureIsSetup = false
                                }
                                .Index(new BlockFetcher(indexer.GetCheckpointRepository().GetCheckpoint("subscriptions"), repo, _Chain));
                        })}).ConfigureAwait(false);

                    await _Configuration.Topics.NewBlocks.AddAsync(header).ConfigureAwait(false);
                }));

            _Disposables.Add(Configuration
               .Topics
               .AddedAddresses
               .CreateConsumer("updater", true)
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
                .OnMessageAsync(async (tx,ctl) =>
                {
                    await Task.WhenAll(new[]{
                        Async(() => indexer.IndexOrderedBalance(tx)),
                        Async(()=>{
                            var txId = tx.GetHash();
                            lock (_Wallets)
                            {
                                var balances =
                                    OrderedBalanceChange
                                    .ExtractWalletBalances(txId, tx, null, null, int.MaxValue, _Wallets)
                                    .AsEnumerable();
                                indexer.Index(balances);
                            }

                            Task notify = null;
                            lock(_Subscriptions)
                            {
                                var topic = Configuration.Topics.SendNotifications;

                                notify = Task.WhenAll(_Subscriptions
                                    .GetNewTransactions()
                                    .Select(t => topic.AddAsync(new Notify()
                                    {
                                        SendAndForget = true,
                                        Notification = new Notification()
                                        {
                                            Subscription = t,
                                            Data = new NewTransactionNotificationData()
                                            {
                                                TransactionId = txId
                                            }
                                        }
                                    })).ToArray());
                                    
                            }
                            notify.Wait();
                        })

                    });
                    var unused = _Configuration.Topics.NewTransactions.AddAsync(tx);
                }, new OnMessageOptions()
                {
                    AutoComplete = true,
                    MaxConcurrentCalls = 10
                }));
        }

        void ErrorHandler(Task task)
        {
            if (task.Exception != null)
            {
                LastException = task.Exception;
            }
        }

        private async Task SendAsync(Notify notify, MessageControl act)
        {
            var n = notify.Notification;
            HttpClient http = new HttpClient();
            var message = new HttpRequestMessage(HttpMethod.Post, n.Subscription.Url);
            n.Tried++;
            var content = new StringContent(n.ToString(), Encoding.UTF8, "application/json");
            message.Content = content;
            CancellationTokenSource tcs = new CancellationTokenSource();
            tcs.CancelAfter(10000);

            var subscription = await Configuration.GetSubscriptionsTable().ReadOneAsync(n.Subscription.Id).ConfigureAwait(false);
            if (subscription == null)
                return;

            bool failed = false;
            try
            {
                var response = await http.SendAsync(message, tcs.Token).ConfigureAwait(false);
                failed = !response.IsSuccessStatusCode;
            }
            catch
            {
                failed = true;
            }
            var tries = new[] 
            { 
                TimeSpan.FromSeconds(0.0),
                TimeSpan.FromMinutes(1.0),
                TimeSpan.FromMinutes(5.0),
                TimeSpan.FromMinutes(30.0),
                TimeSpan.FromMinutes(60.0),
                TimeSpan.FromHours(7.0),
                TimeSpan.FromHours(14.0),
                TimeSpan.FromHours(24.0),
                TimeSpan.FromHours(24.0),
                TimeSpan.FromHours(24.0),
                TimeSpan.FromHours(24.0),
                TimeSpan.FromHours(24.0)
            };

            if (!notify.SendAndForget && failed && (n.Tried - 1) <= tries.Length - 1)
            {
                var wait = tries[n.Tried - 1];
                act.RescheduleIn(wait);
            }
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
