using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.Indexer;
using NBitcoin.Protocol;
using QBitNinja.Models;
using QBitNinja.Notifications;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja
{
    public class QBitTopics
    {
        public QBitTopics(QBitNinjaConfiguration configuration)
        {
            _BroadcastedTransactions = new QBitNinjaTopic<BroadcastedTransaction>(configuration.ServiceBus, new TopicCreation(configuration.Indexer.GetTable("broadcastedtransactions").Name)
            {
                EnableExpress = true
            }, new SubscriptionCreation()
            {
                AutoDeleteOnIdle = TimeSpan.FromHours(24.0)
            });

            _AddedAddresses = new QBitNinjaTopic<WalletAddress[]>(configuration.ServiceBus, new TopicCreation(configuration.Indexer.GetTable("walletrules").Name)
            {
                EnableExpress = true,
                DefaultMessageTimeToLive = TimeSpan.FromMinutes(5.0)
            }, new SubscriptionCreation()
            {
                AutoDeleteOnIdle = TimeSpan.FromHours(24.0)
            });

            _NewBlocks = new QBitNinjaTopic<BlockHeader>(configuration.ServiceBus, new TopicCreation(configuration.Indexer.GetTable("newblocks").Name)
            {
                DefaultMessageTimeToLive = TimeSpan.FromMinutes(5.0),
                EnableExpress = true
            }, new SubscriptionCreation()
            {
                AutoDeleteOnIdle = TimeSpan.FromHours(24.0)
            });

            _NewTransactions = new QBitNinjaTopic<Transaction>(configuration.ServiceBus, new TopicCreation(configuration.Indexer.GetTable("newtransactions").Name)
            {
                DefaultMessageTimeToLive = TimeSpan.FromMinutes(5.0),
            }, new SubscriptionCreation()
            {
                AutoDeleteOnIdle = TimeSpan.FromHours(24.0),
            });

            _SubscriptionChanges = new QBitNinjaTopic<SubscriptionChange>(configuration.ServiceBus, new TopicCreation(configuration.Indexer.GetTable("subscriptionchanges").Name)
            {
                DefaultMessageTimeToLive = TimeSpan.FromMinutes(5.0),
                EnableExpress = true
            });

            _SendNotifications = new QBitNinjaQueue<Notify>(configuration.ServiceBus, new QueueCreation(configuration.Indexer.GetTable("sendnotifications").Name)
            {
                RequiresDuplicateDetection = true,
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10.0),
            });
            _SendNotifications.GetMessageId = (n) => Hashes.Hash256(Encoding.UTF32.GetBytes(n.Notification.ToString())).ToString();


            _InitialIndexing = new QBitNinjaQueue<BlockRange>(configuration.ServiceBus, new QueueCreation(configuration.Indexer.GetTable("intitialindexing").Name)
            {
                RequiresDuplicateDetection = true,
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10.0),
                MaxDeliveryCount = int.MaxValue,
                LockDuration = TimeSpan.FromMinutes(5.0)
            });
            _InitialIndexing.GetMessageId = (n) => n.ToString();
        }


        private QBitNinjaQueue<BlockRange> _InitialIndexing;
        public QBitNinjaQueue<BlockRange> InitialIndexing
        {
            get
            {
                return _InitialIndexing;
            }
        }

        private QBitNinjaQueue<Notify> _SendNotifications;
        public QBitNinjaQueue<Notify> SendNotifications
        {
            get
            {
                return _SendNotifications;
            }
        }

        private QBitNinjaTopic<Transaction> _NewTransactions;
        public QBitNinjaTopic<Transaction> NewTransactions
        {
            get
            {
                return _NewTransactions;
            }
        }

        private QBitNinjaTopic<BlockHeader> _NewBlocks;
        public QBitNinjaTopic<BlockHeader> NewBlocks
        {
            get
            {
                return _NewBlocks;
            }
        }

        QBitNinjaTopic<SubscriptionChange> _SubscriptionChanges;
        public QBitNinjaTopic<SubscriptionChange> SubscriptionChanges
        {
            get
            {
                return _SubscriptionChanges;
            }
        }

        QBitNinjaTopic<BroadcastedTransaction> _BroadcastedTransactions;
        public QBitNinjaTopic<BroadcastedTransaction> BroadcastedTransactions
        {
            get
            {
                return _BroadcastedTransactions;
            }
        }
        private QBitNinjaTopic<WalletAddress[]> _AddedAddresses;
        public QBitNinjaTopic<WalletAddress[]> AddedAddresses
        {
            get
            {
                return _AddedAddresses;
            }
        }

        public IEnumerable<IQBitNinjaQueue> All
        {
            get
            {
                yield return BroadcastedTransactions;
                yield return NewTransactions;
                yield return NewBlocks;
                yield return AddedAddresses;
                yield return SubscriptionChanges;
                yield return SendNotifications;
                yield return InitialIndexing;
            }
        }

        internal Task EnsureSetupAsync()
        {
            return Task.WhenAll(All.Select(a => a.EnsureExistsAsync()).ToArray());
        }
    }
    public class QBitNinjaConfiguration
    {
        public QBitNinjaConfiguration()
        {
            CoinbaseMaturity = 100;
        }
        public static QBitNinjaConfiguration FromConfiguration()
        {
            var conf = new QBitNinjaConfiguration
            {
                Indexer = IndexerConfiguration.FromConfiguration(),
                LocalChain = ConfigurationManager.AppSettings["LocalChain"],
                ServiceBus = ConfigurationManager.AppSettings["ServiceBus"]
            };
			conf.CoinbaseMaturity = conf.Indexer.Network.Consensus.CoinbaseMaturity;
            return conf;
        }

        public IndexerConfiguration Indexer
        {
            get;
            set;
        }

        public string LocalChain
        {
            get;
            set;
        }

        public void EnsureSetup()
        {

            var tasks = new[]
            {
                GetCallbackTable(),
                GetChainCacheCloudTable(),
                GetCrudTable(),
                GetRejectTable().Table,
                GetSubscriptionsTable().Table,
            }.Select(t => t.CreateIfNotExistsAsync()).OfType<Task>().ToList();

            tasks.Add(Indexer.EnsureSetupAsync());
            tasks.Add(Topics.EnsureSetupAsync());

            Task.WaitAll(tasks.ToArray());
        }



        public CrudTable<Subscription> GetSubscriptionsTable()
        {
            return GetCrudTableFactory().GetTable<Subscription>("subscriptions");
        }

        public CrudTable<RejectPayload> GetRejectTable()
        {
            return GetCrudTableFactory().GetTable<RejectPayload>("rejectedbroadcasted");
        }


        QBitTopics _Topics;
        public QBitTopics Topics
        {
            get
            {
                if (_Topics == null)
                    _Topics = new QBitTopics(this);
                return _Topics;
            }
        }



        public CloudTable GetCallbackTable()
        {
            return Indexer.GetTable("callbacks");
        }

        private CloudTable GetCrudTable()
        {
            return Indexer.GetTable("crudtable");
        }

        private CloudTable GetChainCacheCloudTable()
        {
            return Indexer.GetTable("chainchache");
        }


        //////TODO: These methods will need to be in a "RapidUserConfiguration" that need to know about the user for data isolation (using CrudTable.Scope)
       
        public CrudTable<T> GetCacheTable<T>(Scope scope = null)
        {
            return GetCrudTableFactory(scope).GetTable<T>("cache");
        }

        public CrudTableFactory GetCrudTableFactory(Scope scope = null)
        {
            return new CrudTableFactory(GetCrudTable, scope);
        }

        public WalletRepository CreateWalletRepository(Scope scope = null)
        {
            return new WalletRepository(
                    Indexer.CreateIndexerClient(),
                    GetChainCacheTable<BalanceSummary>,
                    GetCrudTableFactory(scope));
        }

        public ChainTable<T> GetChainCacheTable<T>(Scope scope)
        {
            return new ChainTable<T>(GetChainCacheCloudTable())
            {
                Scope = scope
            };
        }

        ///////

        public int CoinbaseMaturity
        {
            get;
            set;
        }

        public string ServiceBus
        {
            get;
            set;
        }
    }
}
