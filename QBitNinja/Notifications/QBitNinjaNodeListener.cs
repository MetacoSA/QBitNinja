using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer;
using NBitcoin.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QBitNinja.Notifications
{
    public class QBitNinjaNodeListener : IDisposable
    {
        private readonly QBitNinjaConfiguration _Configuration;
        public QBitNinjaConfiguration Configuration
        {
            get
            {
                return _Configuration;
            }
        }
        public QBitNinjaNodeListener(QBitNinjaConfiguration configuration)
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

            ListenerTrace.Info("Connecting and handshaking for the sender node...");
            _SenderNode = _Configuration.Indexer.ConnectToNode(false);
            _SenderNode.VersionHandshake();
            _SenderNode.MessageReceived += _SenderNode_MessageReceived;
            ListenerTrace.Info("Sender node handshaked");

            ListenerTrace.Info("Fetching transactions to broadcast...");

            _Disposables.Add(
                Configuration
                .Topics
                .BroadcastedTransactions
                .CreateConsumer()
                .EnsureExists()
                .OnMessage((tx, ctl) =>
                {
                    uint256 hash = null;
                    var repo = Configuration.Indexer.CreateIndexerClient();
                    var rejects = Configuration.GetRejectTable();
                    try
                    {
                        hash = tx.Transaction.GetHash();
                        var indexedTx = repo.GetTransaction(hash);
                        ListenerTrace.Info("Broadcasting " + hash);
                        var reject = rejects.ReadOne(hash.ToString());
                        if (reject != null)
                        {
                            ListenerTrace.Info("Abort broadcasting of rejected");
                            return;
                        }

                        if (_Broadcasting.Count > 1000)
                            _Broadcasting.Clear();

                        _Broadcasting.TryAdd(hash, tx.Transaction);
                        if (indexedTx == null || !indexedTx.BlockIds.Any(id => Chain.Contains(id)))
                        {
                            _SenderNode.SendMessage(new InvPayload(tx.Transaction));
                        }

                        var reschedule = new[]
                        {
                            TimeSpan.FromMinutes(5),
                            TimeSpan.FromMinutes(10),
                            TimeSpan.FromHours(1),
                            TimeSpan.FromHours(6),
                            TimeSpan.FromHours(24),
                        };
                        if (tx.Tried <= reschedule.Length - 1)
                        {
                            ctl.RescheduleIn(reschedule[tx.Tried]);
                            tx.Tried++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LastException = ex;
                        ListenerTrace.Error("Error for new broadcasted transaction " + hash, ex);
                        throw;
                    }
                }));
            ListenerTrace.Info("Transactions to broadcast fetched");



            var ping = new Timer(Ping, null, 0, 1000 * 60);
            _Disposables.Add(ping);
        }


        void Ping(object state)
        {
            ListenerTrace.Verbose("Ping");
            _Node.SendMessage(new PingPayload());
            ListenerTrace.Verbose("Ping");
            _SenderNode.SendMessage(new PingPayload());
        }

        private Node _Node;
        public Node Node
        {
            get
            {
                return _Node;
            }
        }
        private Node _SenderNode;
        public Node SenderNode
        {
            get
            {
                return _SenderNode;
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

        ConcurrentDictionary<uint256, Transaction> _Broadcasting = new ConcurrentDictionary<uint256, Transaction>();
        void _SenderNode_MessageReceived(Node node, IncomingMessage message)
        {
            if (message.Message.Payload is GetDataPayload)
            {
                var getData = (GetDataPayload)message.Message.Payload;
                foreach (var data in getData.Inventory)
                {
                    Transaction tx = null;
                    if (data.Type == InventoryType.MSG_TX && _Broadcasting.TryRemove(data.Hash, out tx))
                    {
                        var payload = new TxPayload(tx);
                        node.SendMessage(payload);
                        ListenerTrace.Info("Broadcasted " + data.Hash);
                    }
                }
            }
            if (message.Message.Payload is RejectPayload)
            {
                var reject = (RejectPayload)message.Message.Payload;
                uint256 txId = reject.Hash;
                if (txId != null)
                {
                    ListenerTrace.Info("Broadcasted transaction rejected (" + reject.Code + ") " + txId);
                    if (reject.Code != RejectCode.DUPLICATE)
                    {
                        Configuration.GetRejectTable().Create(txId.ToString(), reject);
                    }
                    Transaction tx;
                    _Broadcasting.TryRemove(txId, out tx);
                }
            }
            if (message.Message.Payload is PongPayload)
            {
                ListenerTrace.Verbose("Pong");
            }
        }
        void node_MessageReceived(Node node, IncomingMessage message)
        {
            if (message.Message.Payload is InvPayload)
            {
                var inv = (InvPayload)message.Message.Payload;
                foreach (var inventory in inv.Inventory.Where(i => _Broadcasting.ContainsKey(i.Hash)))
                {
                    ListenerTrace.Info("Broadcasted reached mempool " + inventory);
                    Transaction tx;
                    _Broadcasting.TryRemove(inventory.Hash, out tx);
                }
                node.SendMessage(new GetDataPayload(inv.Inventory.ToArray()));
            }
            if (message.Message.Payload is TxPayload)
            {
                var tx = ((TxPayload)message.Message.Payload).Object;
                ListenerTrace.Verbose("Received Transaction " + tx.GetHash());

                Async(() =>
                {
                    _Indexer.Index(new TransactionEntry.Entity(tx.GetHash(), tx, null));
                    var unused = Configuration.Topics.NeedIndexNewTransaction.AddAsync(tx);
                });
            }
            if (message.Message.Payload is BlockPayload)
            {
                var block = ((BlockPayload)message.Message.Payload).Object;
                ListenerTrace.Info("Received block " + block.GetHash());

                Async(() =>
                {
                    var blockId = block.GetHash();
                    node.SynchronizeChain(_Chain);
                    _Indexer.IndexChain(_Chain);
                    ListenerTrace.Info("New height : " + _Chain.Height);
                    var header = _Chain.GetBlock(blockId);
                    if (header == null)
                        return;
                    _Indexer.Index(block);
                    var unused = Configuration.Topics.NeedIndexNewBlock.AddAsync(block.Header);
                });
            }
            if (message.Message.Payload is PongPayload)
            {
                ListenerTrace.Verbose("Pong");
            }
        }

        void Async(Action act)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    act();
                }
                catch (Exception ex)
                {
                    LastException = ex;
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
