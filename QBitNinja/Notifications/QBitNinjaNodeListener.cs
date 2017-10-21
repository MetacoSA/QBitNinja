using Microsoft.WindowsAzure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer;
using NBitcoin.Indexer.IndexTasks;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using QBitNinja.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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
				Logs.Main.LogInformation("State change " + node.State);
				if(node.State == NodeState.HandShaked)
				{
					Logs.Main.LogInformation("Node handshaked");
					AttachedNode.MessageReceived += _Listener.node_MessageReceived;
					AttachedNode.Disconnected += AttachedNode_Disconnected;
					Logs.Main.LogInformation("Connection count : " + NodesGroup.GetNodeGroup(node).ConnectedNodes.Count);
				}
			}

			void AttachedNode_Disconnected(Node node)
			{
				Logs.Main.LogInformation("Node Connection dropped : " + ToString(node.DisconnectReason));
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

		object _LockBalance = new object();
		object _LockTransactions = new object();
		object _LockBlocks = new object();

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

		CustomThreadPoolTaskScheduler _IndexerScheduler;

		List<IDisposable> _Disposables = new List<IDisposable>();
		public void Listen(ConcurrentChain chain = null)
		{
			_Chain = new ConcurrentChain(_Configuration.Indexer.Network);
			_Indexer = Configuration.Indexer.CreateIndexer();
			if(chain == null)
			{
				chain = new ConcurrentChain(_Configuration.Indexer.Network);
			}
			_Chain = chain;
			Logs.Main.LogInformation("Fetching headers from " + _Chain.Tip.Height + " (from azure)");
			var client = Configuration.Indexer.CreateIndexerClient();
			client.SynchronizeChain(chain);
			Logs.Main.LogInformation("Headers fetched tip " + _Chain.Tip.Height);

			_Disposables.Add(_IndexerScheduler = new CustomThreadPoolTaskScheduler(50, 100, "Indexing Threads"));
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
		}

		void ExceptionOnMessagePump(Exception ex)
		{
			if(!_Disposed)
			{
				Logs.Main.LogError(ex, "Error on azure message pumped");
				LastException = ex;
			}
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

		void TryLock(object obj, Action act)
		{
			if(Monitor.TryEnter(obj))
			{
				try
				{
					act();
				}
				finally
				{
					Monitor.Exit(obj);
				}
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

		ConcurrentDictionary<uint256, uint256> _KnownInvs = new ConcurrentDictionary<uint256, uint256>();

		uint256 _LastChainTip;

		void node_MessageReceived(Node node, IncomingMessage message)
		{
			if(_KnownInvs.Count == 1000)
				_KnownInvs.Clear();
			if(message.Message.Payload is InvPayload)
			{
				var inv = (InvPayload)message.Message.Payload;
				var getdata = new GetDataPayload(inv.Inventory.Where(i => i.Type == InventoryType.MSG_TX && _KnownInvs.TryAdd(i.Hash, i.Hash)).ToArray());
				foreach(var data in getdata.Inventory)
				{
					data.Type = node.AddSupportedOptions(InventoryType.MSG_TX);
				}
				if(getdata.Inventory.Count > 0)
					node.SendMessageAsync(getdata);
			}
			if(message.Message.Payload is TxPayload)
			{
				var tx = ((TxPayload)message.Message.Payload).Object;
				Logs.Main.LogDebug("Received Transaction " + tx.GetHash());
				_Indexer.IndexAsync(new TransactionEntry.Entity(tx.GetHash(), tx, null))
						.ContinueWith(HandleException);
				_Indexer.IndexOrderedBalanceAsync(tx)
					.ContinueWith(HandleException);
				Async(() =>
				{
					var txId = tx.GetHash();
					List<OrderedBalanceChange> balances =
						OrderedBalanceChange
						.ExtractWalletBalances(txId, tx, null, null, int.MaxValue, null)
						.AsEnumerable()
						.ToList();

					_Indexer.IndexAsync(balances)
						.ContinueWith(HandleException);
				});
			}

			if(message.Message.Payload is HeadersPayload)
			{
				if(_Chain.Tip.HashBlock != _LastChainTip)
				{
					var header = _Chain.Tip.Header;
					_LastChainTip = _Chain.Tip.HashBlock;

					Configuration.Indexer.CreateIndexer().IndexChain(_Chain);

					Async(() =>
					{
						CancellationTokenSource cancel = new CancellationTokenSource(TimeSpan.FromMinutes(30));
						var repo = new CacheBlocksRepository(new NodeBlocksRepository(node));
						TryLock(_LockBlocks, () =>
						{
							new IndexBlocksTask(Configuration.Indexer)
							{
								EnsureIsSetup = false,

							}.Index(new BlockFetcher(_Indexer.GetCheckpoint(IndexerCheckpoints.Blocks), repo, _Chain)
							{
								CancellationToken = cancel.Token
							}, _Indexer.TaskScheduler);
						});
						TryLock(_LockTransactions, () =>
						{
							new IndexTransactionsTask(Configuration.Indexer)
							{
								EnsureIsSetup = false
							}
							.Index(new BlockFetcher(_Indexer.GetCheckpoint(IndexerCheckpoints.Transactions), repo, _Chain)
							{
								CancellationToken = cancel.Token
							}, _Indexer.TaskScheduler);
						});
						TryLock(_LockBalance, () =>
						{
							new IndexBalanceTask(Configuration.Indexer, null)
							{
								EnsureIsSetup = false
							}.Index(new BlockFetcher(_Indexer.GetCheckpoint(IndexerCheckpoints.Balances), repo, _Chain)
							{
								CancellationToken = cancel.Token
							}, _Indexer.TaskScheduler);
						});
						cancel.Dispose();
					});
				}
			}
		}

		Task Async(Action act)
		{
			var t = new Task(() =>
			{
				try
				{
					act();
				}
				catch(Exception ex)
				{
					if(!_Disposed)
					{
						Logs.Main.LogError("Error during task.", ex);
						LastException = ex;
					}
				}
			});
			t.Start(TaskScheduler.Default);
			return t;
		}

		void HandleException(Task t)
		{
			if(t.IsFaulted)
			{
				if(!_Disposed)
				{
					Logs.Main.LogError("Error during asynchronous task", t.Exception);
					LastException = t.Exception;
				}
			}
		}

		public Exception LastException
		{
			get;
			set;
		}

		volatile bool _Disposed;
		#region IDisposable Members

		public void Dispose()
		{
			_Disposed = true;
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
