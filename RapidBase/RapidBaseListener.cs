using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.Indexer;
using NBitcoin.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RapidBase
{
    public class RapidBaseListener : IDisposable
    {
        private readonly RapidBaseConfiguration _Configuration;
        public RapidBaseConfiguration Configuration
        {
            get
            {
                return _Configuration;
            }
        }
        public RapidBaseListener(RapidBaseConfiguration configuration)
        {
            _Configuration = configuration;
        }

        private AzureIndexer _Indexer;
        public AzureIndexer Indexer
        {
            get
            {
                return _Indexer;
            }
        }

        List<IDisposable> _Disposables = new List<IDisposable>();
        SingleThreadTaskScheduler _Scheduler;
        public void Listen()
        {
            _Evt.Reset();
            _Scheduler = new SingleThreadTaskScheduler();
            ListenerTrace.Info("Connecting to node " + Configuration.Indexer.Node + "...");
            _Node = _Configuration.Indexer.ConnectToNode(true);
            ListenerTrace.Info("Connected");
            ListenerTrace.Info("Handshaking...");
            _Node.VersionHandshake();
            ListenerTrace.Info("Handshaked");
            _Chain = new ConcurrentChain(_Configuration.Indexer.Network);
            ListenerTrace.Info("Fetching headers...");
            _Node.SynchronizeChain(_Chain);
            ListenerTrace.Info("Headers fetched tip " + _Chain.Tip.Height);
            _Indexer = Configuration.Indexer.CreateIndexer();
            ListenerTrace.Info("Indexing indexer chain...");
            _Indexer.IndexChain(_Chain);
            _Node.MessageReceived += node_MessageReceived;
            _Wallets = _Configuration.Indexer.CreateIndexerClient().GetAllWalletRules();

            ListenerTrace.Info("Fetching transactions to broadcast...");
            _Disposables.Add(Configuration
                .GetBroadcastedTransactionsListenable()
                .CreateConsumer()
                .EnsureExists()
                .OnMessage(evt =>
                {
                    uint256 hash = null;
                    try
                    {
                        var tx = new BroadcastedTransaction(evt.AddedEntity);
                        hash = tx.Transaction.GetHash();
                        var value = _BroadcastedTransactions.GetOrAdd(hash, tx);
                        ListenerTrace.Info("Broadcasting " + hash);
                        if (value == tx) //Was not present before
                        {
                            _Node.SendMessage(new InvPayload(tx.Transaction));
                        }
                    }
                    catch (Exception ex)
                    {
                        LastException = ex;
                        ListenerTrace.Error("Error for new broadcasted transaction " + hash, ex);
                    }
                }));
            ListenerTrace.Info("Transactions to broadcast fetched");
            ListenerTrace.Info("Fetching wallet rules...");
            _Disposables.Add(Configuration
               .GetWalletRuleListenable()
               .CreateConsumer()
               .EnsureExists()
               .OnMessage(evt =>
               {
                   ListenerTrace.Info("New wallet rule");
                   RunTask("New wallet rule", () =>
                   {
                       _Wallets.Add(new WalletRuleEntry(evt.AddedEntity, Configuration.Indexer.CreateIndexerClient()));
                   }, true);
               }));
            ListenerTrace.Info("Wallet rules fetched");
        }

        ConcurrentDictionary<uint256, BroadcastedTransaction> _BroadcastedTransactions = new ConcurrentDictionary<uint256, BroadcastedTransaction>();

        private Node _Node;
        public Node Node
        {
            get
            {
                return _Node;
            }
        }

        private ConcurrentChain _Chain;
        public ConcurrentChain Chain
        {
            get
            {
                return _Chain;
            }
        }

        void node_MessageReceived(Node node, IncomingMessage message)
        {
            if (message.Message.Payload is GetDataPayload)
            {
                var getData = (GetDataPayload)message.Message.Payload;
                foreach (var data in getData.Inventory)
                {
                    if (data.Type == InventoryType.MSG_TX && _BroadcastedTransactions.ContainsKey(data.Hash))
                    {
                        var result = _BroadcastedTransactions[data.Hash];
                        var tx = new TxPayload(result.Transaction);
                        node.SendMessage(tx);
                        ListenerTrace.Info("Broadcasted " + data.Hash);
                        try
                        {
                            Configuration.GetBroadcastedTransactionsListenable().CloudTable.Execute(TableOperation.Delete(result.ToEntity()));
                        }
                        catch (StorageException)
                        {
                        }
                    }
                }
            }
            if (message.Message.Payload is InvPayload)
            {
                var inv = (InvPayload)message.Message.Payload;
                node.SendMessage(new GetDataPayload(inv.Inventory.ToArray()));
            }
            if (message.Message.Payload is TxPayload)
            {
                var tx = ((TxPayload)message.Message.Payload).Object;
                ListenerTrace.Verbose("Received Transaction " + tx.GetHash());
                RunTask("New transaction", () =>
                {
                    var txId = tx.GetHash();
                    _Indexer.Index(new TransactionEntry.Entity(txId, tx, null));
                    _Indexer.IndexOrderedBalance(tx);
                    RunTask("New transaction", () =>
                    {
                        _Indexer.Index(OrderedBalanceChange.ExtractWalletBalances(txId, tx, null, null, int.MaxValue, _Wallets));
                    }, true);
                }, false);
            }
            if (message.Message.Payload is BlockPayload)
            {
                var block = ((BlockPayload)message.Message.Payload).Object;
                ListenerTrace.Info("Received block " + block.GetHash());
                RunTask("New block", () =>
                {
                    var blockId = block.GetHash();
                    node.SynchronizeChain(_Chain);
                    _Indexer.IndexChain(_Chain);
                    ListenerTrace.Info("New height : " + _Chain.Height);
                    var header = _Chain.GetBlock(blockId);
                    if (header == null)
                        return;
                    _Indexer.IndexWalletOrderedBalance(header.Height, block, _Wallets);

                    RunTask("New block", () =>
                    {
                        _Indexer.Index(block);
                    }, false);
                    RunTask("New block", () =>
                    {
                        _Indexer.IndexTransactions(header.Height, block);
                    }, false);
                    RunTask("New block", () =>
                    {
                        _Indexer.IndexOrderedBalance(header.Height, block);
                    }, false);
                }, true);
            }
        }


        WalletRuleEntryCollection _Wallets = null;


        void RunTask(string name, Action act, bool commonThread)
        {
            new Task(() =>
            {
                try
                {
                    act();
                }
                catch (Exception ex)
                {
                    ListenerTrace.Error("Error during task : " + name, ex);
                    LastException = ex;
                }
            }).Start(commonThread ? _Scheduler : TaskScheduler.Default);
        }

        public Exception LastException
        {
            get;
            set;
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_Scheduler != null)
            {
                _Scheduler.Dispose();
                _Scheduler = null;
            }
            if (_Node != null)
            {
                _Node.Dispose();
                _Node = null;
            }
            foreach (var dispo in _Disposables)
                dispo.Dispose();
            _Disposables.Clear();
            _Evt.Set();
        }

        #endregion
        ManualResetEvent _Evt = new ManualResetEvent(true);
        public void Wait()
        {
            _Evt.WaitOne();
        }
    }
}
