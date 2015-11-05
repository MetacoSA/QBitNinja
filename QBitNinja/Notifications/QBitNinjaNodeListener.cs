using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QBitNinja.Notifications
{
    public class QBitNinjaNodeListener : IDisposable
    {
        class Behavior : NodeBehavior
        {
            QBitNinjaNodeListener _Listener;
            public Behavior(QBitNinjaNodeListener listener)
            {
                _Listener = listener;
            }

            protected override void AttachCore()
            {
                AttachedNode.StateChanged += AttachedNode_StateChanged;
            }

            void AttachedNode_StateChanged(Node node, NodeState oldState)
            {
                ListenerTrace.Info("State change " + node.State);
                if(node.State == NodeState.HandShaked)
                {
                    ListenerTrace.Info("Node handshaked");
                    ListenerTrace.Info("Fetching headers...");
                    AttachedNode.SynchronizeChain(_Listener._Chain);
                    _Listener._Indexer.IndexChain(_Listener._Chain);
                    ListenerTrace.Info("Headers fetched tip " + _Listener._Chain.Tip.Height);
                    AttachedNode.MessageReceived += _Listener.node_MessageReceived;
                    AttachedNode.Disconnected += AttachedNode_Disconnected;
                    ListenerTrace.Info("Connection count : " + NodesGroup.GetNodeGroup(node).ConnectedNodes.Count);
                }
            }

            void AttachedNode_Disconnected(Node node)
            {
                ListenerTrace.Info("Node Connection dropped : " + ToString(node.DisconnectReason));
            }

            private string ToString(NodeDisconnectReason reason)
            {
                if(reason == null)
                    return null;
                return reason.Reason + " " + reason.Exception == null ? "" : Utils.ExceptionToString(reason.Exception);
            }

            protected override void DetachCore()
            {
                AttachedNode.StateChanged -= AttachedNode_StateChanged;
                AttachedNode.MessageReceived -= _Listener.node_MessageReceived;
            }

            public override object Clone()
            {
                return new Behavior(_Listener);
            }
        }
        private readonly QBitNinjaConfiguration _Configuration;
        public QBitNinjaConfiguration Configuration
        {
            get
            {
                return _Configuration;
            }
        }
        SingleThreadTaskScheduler _SingleThreadTaskScheduler;
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

        CustomThreadPoolTaskScheduler _IndexerScheduler;

        List<IDisposable> _Disposables = new List<IDisposable>();
        public void Listen()
        {
            _Chain = new ConcurrentChain(_Configuration.Indexer.Network);

            _Indexer = Configuration.Indexer.CreateIndexer();
            _Disposables.Add(_IndexerScheduler = new CustomThreadPoolTaskScheduler(30, 20));
            _Indexer.TaskScheduler = _IndexerScheduler;

            _Group = new NodesGroup(Configuration.Indexer.Network);
            _Disposables.Add(_Group);
            _Group.AllowSameGroup = true;
            _Group.MaximumNodeConnection = 2;
            AddressManager addrman = new AddressManager();
            addrman.Add(new NetworkAddress(Utils.ParseIpEndpoint(_Configuration.Indexer.Node, Configuration.Indexer.Network.DefaultPort)),
                        IPAddress.Parse("127.0.0.1"));
            _Group.NodeConnectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(addrman));
            _Group.NodeConnectionParameters.TemplateBehaviors.Add(new ChainBehavior(_Chain));
            _Group.NodeConnectionParameters.TemplateBehaviors.Add(new Behavior(this));
            _Group.Connect();

            ListenerTrace.Info("Fetching transactions to broadcast...");

            _Disposables.Add(_SingleThreadTaskScheduler = new SingleThreadTaskScheduler());

            _Disposables.Add(
                Configuration
                .Topics
                .BroadcastedTransactions
                .CreateConsumer("listener", true)
                .EnsureSubscriptionExists()
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
                        if(reject != null)
                        {
                            ListenerTrace.Info("Abort broadcasting of rejected");
                            return;
                        }

                        if(_Broadcasting.Count > 1000)
                            _Broadcasting.Clear();

                        _Broadcasting.TryAdd(hash, tx.Transaction);
                        if(indexedTx == null || !indexedTx.BlockIds.Any(id => Chain.Contains(id)))
                        {
                            var unused = SendMessageAsync(new InvPayload(tx.Transaction));
                        }

                        var reschedule = new[]
                        {
                            TimeSpan.FromMinutes(5),
                            TimeSpan.FromMinutes(10),
                            TimeSpan.FromHours(1),
                            TimeSpan.FromHours(6),
                            TimeSpan.FromHours(24),
                        };
                        if(tx.Tried <= reschedule.Length - 1)
                        {
                            ctl.RescheduleIn(reschedule[tx.Tried]);
                            tx.Tried++;
                        }
                    }
                    catch(Exception ex)
                    {
                        LastException = ex;
                        ListenerTrace.Error("Error for new broadcasted transaction " + hash, ex);
                        throw;
                    }
                }));
            ListenerTrace.Info("Transactions to broadcast fetched");
        }

        NodesGroup _Group;
        private async Task SendMessageAsync(Payload payload)
        {
            int[] delays = new int[] { 50, 100, 200, 300, 1000, 2000, 3000, 6000, 12000 };
            int i = 0;
            while(_Group.ConnectedNodes.Count != 2)
            {
                i++;
                i = Math.Min(i, delays.Length - 1);
                await Task.Delay(delays[i]).ConfigureAwait(false);
            }
            await _Group.ConnectedNodes.First().SendMessageAsync(payload).ConfigureAwait(false);
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
        ConcurrentDictionary<uint256, uint256> _KnownInvs = new ConcurrentDictionary<uint256, uint256>();
        uint256 _LastBlock;

        void node_MessageReceived(Node node, IncomingMessage message)
        {
            if(_KnownInvs.Count == 1000)
                _KnownInvs.Clear();
            if(message.Message.Payload is InvPayload)
            {
                var inv = (InvPayload)message.Message.Payload;
                foreach(var inventory in inv.Inventory)
                {
                    Transaction tx;
                    if(_Broadcasting.TryRemove(inventory.Hash, out tx))
                        ListenerTrace.Info("Broadcasted reached mempool " + inventory);
                }
                var getdata = new GetDataPayload(inv.Inventory.Where(i => i.Type == InventoryType.MSG_TX && _KnownInvs.TryAdd(i.Hash, i.Hash)).ToArray());

                if(getdata.Inventory.Count > 0)
                    node.SendMessageAsync(getdata);

                _LastBlock = inv.Inventory
                                .Where(i => i.Type == InventoryType.MSG_BLOCK)
                                .Select(i=>i.Hash).FirstOrDefault() ?? _LastBlock;
            }
            if(message.Message.Payload is TxPayload)
            {
                var tx = ((TxPayload)message.Message.Payload).Object;
                ListenerTrace.Verbose("Received Transaction " + tx.GetHash());

                Async(() =>
                {
                    Async(() =>
                    {
                        _Indexer.Index(new TransactionEntry.Entity(tx.GetHash(), tx, null));
                        var unused = Configuration.Topics.NeedIndexNewTransaction.AddAsync(tx);
                    }, false);
                }, true);
            }

            if(message.Message.Payload is HeadersPayload)
            {
                var header = ((HeadersPayload)message.Message.Payload).Headers.FirstOrDefault(h => _LastBlock != null && h.GetHash() == _LastBlock);
                if(header != null)
                {
                    var unused = Configuration.Topics.NeedIndexNewBlock.AddAsync(header);
                }
            }
            if(message.Message.Payload is GetDataPayload)
            {
                var getData = (GetDataPayload)message.Message.Payload;
                foreach(var data in getData.Inventory)
                {
                    Transaction tx = null;
                    if(data.Type == InventoryType.MSG_TX && _Broadcasting.TryGetValue(data.Hash, out tx))
                    {
                        var payload = new TxPayload(tx);
                        node.SendMessageAsync(payload);
                        ListenerTrace.Info("Broadcasted " + data.Hash);
                    }
                }
            }
            if(message.Message.Payload is RejectPayload)
            {
                var reject = (RejectPayload)message.Message.Payload;
                uint256 txId = reject.Hash;
                if(txId != null)
                {
                    ListenerTrace.Info("Broadcasted transaction rejected (" + reject.Code + ") " + txId);
                    if(reject.Code != RejectCode.DUPLICATE)
                    {
                        Configuration.GetRejectTable().Create(txId.ToString(), reject);
                    }
                    Transaction tx;
                    _Broadcasting.TryRemove(txId, out tx);
                }
            }
        }

        void Async(Action act, bool commonThread)
        {
            new Task(() =>
            {
                try
                {
                    act();
                }
                catch(Exception ex)
                {
                    if(ex is ObjectDisposedException)
                        return;
                    ListenerTrace.Error("Error during task.", ex);
                    LastException = ex;
                }
            }).Start(commonThread ? _SingleThreadTaskScheduler : TaskScheduler.Default);
        }

        public Exception LastException
        {
            get;
            set;
        }

        #region IDisposable Members

        public void Dispose()
        {
            foreach(var dispo in _Disposables)
                dispo.Dispose();
            _Disposables.Clear();
            if(LastException == null)
                _Finished.SetResult(true);
            else
                _Finished.SetException(LastException);
        }

        #endregion
        TaskCompletionSource<bool> _Finished = new TaskCompletionSource<bool>();
        public Task Running
        {
            get
            {
                return _Finished.Task;
            }
        }
    }
}
