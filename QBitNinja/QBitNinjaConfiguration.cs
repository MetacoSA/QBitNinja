using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.Indexer;
using NBitcoin.Protocol;
using QBitNinja.Models;
using QBitNinja.Notifications;
using System;
using System.Configuration;
using System.Linq;
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

            _AddedAddresses = new QBitNinjaTopic<WalletAddress>(configuration.ServiceBus, new TopicCreation(configuration.Indexer.GetTable("walletrules").Name)
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

            _NeedIndexNewBlock = new QBitNinjaQueue<BlockHeader>(configuration.ServiceBus, new QueueCreation(configuration.Indexer.GetTable("newindexblocks").Name)
            {
                
                DefaultMessageTimeToLive = TimeSpan.FromMinutes(5.0),
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(5.0),
                RequiresDuplicateDetection = true,
            });
            _NeedIndexNewBlock.GetMessageId = (header) => header.GetHash().ToString();


            _NeedIndexNewTransaction = new QBitNinjaQueue<Transaction>(configuration.ServiceBus, new QueueCreation(configuration.Indexer.GetTable("newindextxs").Name)
            {

                DefaultMessageTimeToLive = TimeSpan.FromMinutes(5.0),
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(5.0),
                RequiresDuplicateDetection = true,
            });
            _NeedIndexNewTransaction.GetMessageId = (header) => header.GetHash().ToString();
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

        QBitNinjaQueue<BlockHeader> _NeedIndexNewBlock;
        public QBitNinjaQueue<BlockHeader> NeedIndexNewBlock
        {
            get
            {
                return _NeedIndexNewBlock;
            }
        }

        QBitNinjaQueue<Transaction> _NeedIndexNewTransaction;
        public QBitNinjaQueue<Transaction> NeedIndexNewTransaction
        {
            get
            {
                return _NeedIndexNewTransaction;
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
        private QBitNinjaTopic<WalletAddress> _AddedAddresses;
        public QBitNinjaTopic<WalletAddress> AddedAddresses
        {
            get
            {
                return _AddedAddresses;
            }
        }

        internal Task EnsureSetupAsync()
        {
            return Task.WhenAll(new[] { 
                BroadcastedTransactions.EnsureSetupAsync(), 
                NewTransactions.EnsureSetupAsync(),
                NewBlocks.EnsureSetupAsync(),
                AddedAddresses.EnsureSetupAsync(),
                NeedIndexNewBlock.EnsureSetupAsync(),
                NeedIndexNewTransaction.EnsureSetupAsync()
            });
        }
    }
    public class QBitNinjaConfiguration
    {
        public QBitNinjaConfiguration()
        {
            CoinbaseMaturity = 101;
        }
        public static QBitNinjaConfiguration FromConfiguration()
        {
            var conf = new QBitNinjaConfiguration
            {
                Indexer = IndexerConfiguration.FromConfiguration(),
                LocalChain = ConfigurationManager.AppSettings["LocalChain"],
                ServiceBus = ConfigurationManager.AppSettings["ServiceBus"]
            };
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
            Indexer.EnsureSetup();

            var tasks = new[]
            {
                GetCallbackTable(),
                GetChainCacheCloudTable(),
                GetCrudTable(),
                GetRejectTable().Table
            }.Select(t => t.CreateIfNotExistsAsync()).ToArray();

            var tasks2 = new Task[]
            { 
                Topics.EnsureSetupAsync(),
            };
            Task.WaitAll(tasks.Concat(tasks2).ToArray());
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
