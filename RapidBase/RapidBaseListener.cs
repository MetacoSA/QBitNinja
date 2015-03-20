using NBitcoin;
using NBitcoin.Indexer;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        SingleThreadTaskScheduler _Scheduler;
        public void Listen()
        {
            _Scheduler = new SingleThreadTaskScheduler();
            _Node = _Configuration.Indexer.ConnectToNode(true);
            _Node.VersionHandshake();
            _Chain = new ConcurrentChain(_Configuration.Indexer.Network);
            _Node.SynchronizeChain(_Chain);
            _Indexer = Configuration.Indexer.CreateIndexer();
            _Indexer.IndexChain(_Chain);
            _Node.MessageReceived += node_MessageReceived;
            _Wallets = _Configuration.Indexer.CreateIndexerClient().GetAllWalletRules();
        }

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
            if (message.Message.Payload is InvPayload)
            {
                var inv = (InvPayload)message.Message.Payload;
                node.SendMessage(new GetDataPayload(inv.Inventory.ToArray()));
            }
            if (message.Message.Payload is TxPayload)
            {
                var tx = ((TxPayload)message.Message.Payload).Object;
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
                RunTask("New block", () =>
                {
                    var blockId = block.GetHash();
                    node.SynchronizeChain(_Chain);
                    _Indexer.IndexChain(_Chain);
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
                    RapidBaseListenerTrace.Error("Error during task : " + name, ex);
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
                _Scheduler.Dispose();
            if (_Node != null)
                _Node.Dispose();
        }

        #endregion
    }
}
