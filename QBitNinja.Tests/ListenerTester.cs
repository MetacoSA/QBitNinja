using NBitcoin;
using NBitcoin.Indexer;
using NBitcoin.Protocol;
using QBitNinja.Notifications;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace QBitNinja.Tests
{
    public class ListenerTester : IDisposable
    {
        ServerTester _Server;
        internal Network _Network;

        public ListenerTester(ServerTester tester, bool nodeOnly)
        {
            _Network = tester.Configuration.Indexer.Network;
            Random rand = new Random();
            _Server = tester;
            _Server._disposables.Add(this);

            _Listener = new QBitNinjaNodeListener(_Server.Configuration);

            _NodeServer = new NodeServer(_Server.Configuration.Indexer.Network, internalPort: rand.Next(20000, 50000));
            _NodeListener = new EventLoopMessageListener<IncomingMessage>(NewNodeMessage);
            _NodeServer.ExternalEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), _NodeServer.LocalEndpoint.Port);
            _NodeServer.AllowLocalPeers = true;
            _NodeServer.IsRelay = true;
            _NodeServer.AllMessages.AddMessageListener(_NodeListener);
            _NodeServer.Listen();

            _Listener.Configuration.Indexer.Node = "127.0.0.1:" + _NodeServer.LocalEndpoint.Port;
            if (!nodeOnly)
                _Listener.Listen();

            _Updater = new BlocksUpdater(_Listener.Configuration);
            if (!nodeOnly)
                _Updater.Listen();

            _Server.Configuration.Indexer.CreateIndexer().Index(_Server.Configuration.Indexer.Network.GetGenesis());

            _Server.ChainBuilder.SkipIndexer = true;
            _Server.ChainBuilder.NewBlock += ChainBuilder_NewBlock;
            _Server.ChainBuilder.NewTransaction += ChainBuilder_NewTransaction;

            var genesis = _Server.Configuration.Indexer.Network.GetGenesis();
            _Blocks.AddOrReplace(genesis.Header.GetHash(), genesis);
        }

        private readonly BlocksUpdater _Updater;
        public BlocksUpdater Updater
        {
            get
            {
                return _Updater;
            }
        }

        void ChainBuilder_NewBlock(Block obj)
        {
            _Blocks.AddOrUpdate(obj.GetHash(), obj, (a, b) => b);
            foreach (var node in _Nodes)
            {
                node.SendMessage(new InvPayload(obj));
            }
        }

        void ChainBuilder_NewTransaction(Transaction obj)
        {
            _Transactions.AddOrUpdate(obj.GetHash(), obj, (a, b) => b);
            foreach (var node in _Nodes)
            {
                node.SendMessage(new InvPayload(obj));
            }
        }

        void NewNodeMessage(IncomingMessage message)
        {
            if (message.Message.Payload is VerAckPayload)
            {
                _Nodes.Add(message.Node);
            }
            if (message.Message.Payload is InvPayload)
            {
                InvPayload invPayload = (InvPayload)message.Message.Payload;
                if (_Reject == null)
                    message.Node.SendMessage(new GetDataPayload(invPayload.Inventory.ToArray()));
                else
                {
                    _Reject.Message = "tx";
                    _Reject.Hash = invPayload.Inventory[0].Hash;
                    message.Node.SendMessage(_Reject);
                }
            }
            if (message.Message.Payload is TxPayload)
            {
                TxPayload txPayload = (TxPayload)message.Message.Payload;
                _ReceivedTransactions.AddOrUpdate(txPayload.Object.GetHash(), txPayload.Object, (k, v) => v);
                _Transactions.AddOrUpdate(txPayload.Object.GetHash(), txPayload.Object, (k, v) => v);
                foreach (var node in _Nodes)
                {
                    if (node != message.Node)
                        node.SendMessage(new InvPayload(txPayload.Object));
                }
            }
            if (message.Message.Payload is GetHeadersPayload)
            {
                var headers = (GetHeadersPayload)message.Message.Payload;
                var fork = _Server.ChainBuilder.Chain.FindFork(headers.BlockLocators);
                var response =
                    _Server.ChainBuilder.Chain
                    .ToEnumerable(true)
                    .TakeWhile(f => f.HashBlock != fork.HashBlock && f.HashBlock != headers.HashStop)
                    .Select(f => f.Header)
                    .Reverse()
                    .ToArray();
                HeadersPayload res = new HeadersPayload();
                res.Headers.AddRange(response);
                message.Node.SendMessage(res);
            }

            if (message.Message.Payload is GetDataPayload)
            {
                Transaction tx;
                Block block;
                var getData = message.Message.Payload as GetDataPayload;
                foreach (var inv in getData.Inventory)
                {
                    if (inv.Type == InventoryType.MSG_TX)
                        if (_Transactions.TryGetValue(inv.Hash, out tx))
                        {
                            message.Node.SendMessage(new TxPayload(tx));
                        }
                    if (inv.Type == InventoryType.MSG_BLOCK)
                    {
                        if (_Blocks.TryGetValue(inv.Hash, out block))
                        {
                            message.Node.SendMessage(new BlockPayload(block));
                        }
                        else
                        {
                            
                        }
                    }
                }
            }
        }

        public void AssertReceivedTransaction(uint256 txId)
        {
            CancellationTokenSource s = new CancellationTokenSource();
            s.CancelAfter(5000);
            while (!_ReceivedTransactions.ContainsKey(txId))
            {
                if (s.IsCancellationRequested)
                    Assert.False(true);
            }
        }

        List<Node> _Nodes = new List<Node>();
        ConcurrentDictionary<uint256, Transaction> _ReceivedTransactions = new ConcurrentDictionary<uint256, Transaction>();
        ConcurrentDictionary<uint256, Transaction> _Transactions = new ConcurrentDictionary<uint256, Transaction>();
        ConcurrentDictionary<uint256, Block> _Blocks = new ConcurrentDictionary<uint256, Block>();

        private readonly NodeServer _NodeServer;
        public NodeServer NodeServer
        {
            get
            {
                return _NodeServer;
            }
        }

        private readonly QBitNinjaNodeListener _Listener;
        private EventLoopMessageListener<IncomingMessage> _NodeListener;
        public QBitNinjaNodeListener Listener
        {
            get
            {
                return _Listener;
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_Listener != null)
                _Listener.Dispose();
            if (_Updater != null)
                _Updater.Dispose();
            if (_NodeServer != null)
                _NodeServer.Dispose();
            if (_NodeListener != null)
                _NodeListener.Dispose();
            Assert.Null(Listener.LastException);
            Assert.Null(Updater.LastException);
        }

        #endregion

        volatile RejectPayload _Reject;
        public void Reject(RejectPayload reject)
        {
            _Reject = reject;
        }



        internal async Task WaitMessageAsync<T>(QBitNinjaTopic<T> topic) where T : class
        {
            var message = await topic.CreateConsumer(new SubscriptionCreation()
            {
                Name = "test",
                DefaultMessageTimeToLive = TimeSpan.FromSeconds(1.0),
                AutoDeleteOnIdle = TimeSpan.FromMinutes(5.0)
            })
            .EnsureSubscriptionExists()
            .ReceiveAsync(TimeSpan.FromSeconds(10.0)).ConfigureAwait(false);
            if (message == null)
                Assert.True(false, "No message received on topic");
        }
    }
}
