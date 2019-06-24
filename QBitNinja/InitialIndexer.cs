using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer;
using NBitcoin.Indexer.IndexTasks;
using QBitNinja.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using NBitcoin.Protocol;

namespace QBitNinja
{
    public class BlockRange
    {
        public string Target { get; set; }
        public int From { get; set; }
        public int Count { get; set; }
        public bool Processed { get; set; }
        public override string ToString() => Target + "- " + From + "-" + Count;
    }


    public class InitialIndexer
    {

        private QBitNinjaConfiguration _Conf;
        private Dictionary<string, Tuple<Checkpoint, IIndexTask>> _IndexTasks = new Dictionary<string, Tuple<Checkpoint, IIndexTask>>();

        public int BlockGranularity { get; set; }
        public int TransactionsPerWork { get; set; }

        public InitialIndexer(QBitNinjaConfiguration conf)
        {
            _Conf = conf ?? throw new ArgumentNullException("conf");
            BlockGranularity = 100;
            TransactionsPerWork = 1000 * 1000;
            Init();
        }

        private void Init()
        {
            AzureIndexer indexer = _Conf.Indexer.CreateIndexer();
            AddTaskIndex(indexer.GetCheckpoint(IndexerCheckpoints.Balances), new IndexBalanceTask(_Conf.Indexer, null));
            AddTaskIndex(indexer.GetCheckpoint(IndexerCheckpoints.Blocks), new IndexBlocksTask(_Conf.Indexer));
            AddTaskIndex(indexer.GetCheckpoint(IndexerCheckpoints.Transactions), new IndexTransactionsTask(_Conf.Indexer));
            AddTaskIndex(indexer.GetCheckpoint(IndexerCheckpoints.Wallets), new IndexBalanceTask(_Conf.Indexer, _Conf.Indexer.CreateIndexerClient().GetAllWalletRules()));
            Checkpoint subscription = indexer.GetCheckpointRepository().GetCheckpoint("subscriptions");
            AddTaskIndex(subscription, new IndexNotificationsTask(_Conf, new SubscriptionCollection(_Conf.GetSubscriptionsTable().Read())));
        }

        private void AddTaskIndex(Checkpoint checkpoint, IIndexTask indexTask)
        {
            _IndexTasks.Add(checkpoint.CheckpointName, Tuple.Create(checkpoint, indexTask));
        }


        public void Cancel()
        {
            CloudBlockBlob blobLock = GetInitBlob();
            try
            {
                try
                {
                    blobLock.Delete();
                }
                catch
                {
                    blobLock.BreakLease();
                    blobLock.Delete();
                }
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation == null || ex.RequestInformation.HttpStatusCode != 404)
                {
                    throw;
                }
            }

            ListenerTrace.Info("Init blob deleted");
            ListenerTrace.Info("Deleting queue...");
            _Conf.Topics
                 .InitialIndexing
                 .GetNamespace().DeleteQueue(_Conf.Topics.InitialIndexing.Queue);
            ListenerTrace.Info("Queue deleted");
        }

        public int Run(ChainBase chain = null)
        {
            ListenerTrace.Info("Start initial indexing");
            var totalProcessed = 0;
            using (Node node = _Conf.Indexer.ConnectToNode(false))
            {
                ListenerTrace.Info("Handshaking...");
                node.VersionHandshake();
                ListenerTrace.Info("Handshaked");
                chain = chain ?? node.GetChain();
                ListenerTrace.Info("Current chain at height " + chain.Height);
                NodeBlocksRepository blockRepository = new NodeBlocksRepository(node);
                CloudBlockBlob blobLock = GetInitBlob();
                string lease = null;
                try
                {
                    blobLock.UploadText("Enqueuing");
                    lease = blobLock.AcquireLease(null, null);
                }
                catch (StorageException)
                {
                }

                if (lease != null)
                {
                    ListenerTrace.Info("Queueing index jobs");
                    EnqueueJobs(blockRepository, chain, blobLock, lease);
                }

                ListenerTrace.Info("Dequeuing index jobs");

                while (true)
                {
                    QBitNinjaMessage<BlockRange> msg = _Conf.Topics
                       .InitialIndexing
                       .ReceiveAsync(TimeSpan.FromMilliseconds(1000))
                       .Result;

                    NamespaceManager ns = _Conf.Topics.InitialIndexing.GetNamespace();
                    QueueDescription description = ns.GetQueue(_Conf.Topics.InitialIndexing.Queue);

                    Console.WriteLine("Work remaining in the queue : " + description.MessageCountDetails.ActiveMessageCount);
                    if (msg == null)
                    {
                        string state = blobLock.DownloadText();
                        if (state != "Enqueuing" && description.MessageCountDetails.ActiveMessageCount == 0)
                        {
                            BlockLocator locator = new BlockLocator();
                            locator.FromBytes(Encoders.Hex.DecodeData(state));
                            UpdateCheckpoints(locator);
                            break;
                        }

                        ListenerTrace.Info("Additional work will be enqueued...");
                        continue;
                    }

                    using (msg.Message)
                    {
                        BlockRange range = msg.Body;
                        using (CustomThreadPoolTaskScheduler scheduler = new CustomThreadPoolTaskScheduler(50, 100, range.ToString()))
                        {
                            ListenerTrace.Info("Processing " + range);
                            totalProcessed++;
                            Tuple<Checkpoint, IIndexTask> task = _IndexTasks[range.Target];
                            BlockFetcher fetcher = new BlockFetcher(task.Item1, blockRepository, chain)
                            {
                                FromHeight = range.From,
                                ToHeight = range.From + range.Count - 1
                            };

                            try
                            {
                                task.Item2.SaveProgression = false;
                                task.Item2.EnsureIsSetup = totalProcessed == 0;
                                Task index = Task.Factory.StartNew(
                                    () => { task.Item2.Index(fetcher, scheduler); },
                                    TaskCreationOptions.LongRunning);

                                while (!index.Wait(TimeSpan.FromMinutes(4)))
                                {
                                    msg.Message.RenewLock();
                                    ListenerTrace.Info("Lock renewed");
                                }
                            }
                            catch (AggregateException aex)
                            {
                                ExceptionDispatchInfo.Capture(aex.InnerException ?? aex).Throw();
                                throw;
                            }

                            range.Processed = true;
                            msg.Message.Complete();
                        }
                    }
                }
            }

            ListenerTrace.Info("Initial indexing terminated");
            return totalProcessed;
        }

        private CloudBlockBlob GetInitBlob()
        {
            CloudBlobContainer container = _Conf.Indexer.GetBlocksContainer();
            CloudBlockBlob blobLock = container.GetBlockBlobReference("initialindexer/lock");
            return blobLock;
        }

        private void UpdateCheckpoints(BlockLocator locator)
        {
            ListenerTrace.Info("Work finished, updating checkpoints");
            foreach (Checkpoint chk in _IndexTasks.Select(kv => kv.Value.Item1))
            {
                ListenerTrace.Info(chk.CheckpointName + "...");
                chk.SaveProgress(locator);
            }

            ListenerTrace.Info("Checkpoints updated");
        }

        private void EnqueueJobs(NodeBlocksRepository repo, ChainBase chain, CloudBlockBlob blobLock, string lease)
        {
            var cumul = 0;
            ChainedBlock from = chain.Genesis;
            var blockCount = 0;
            foreach (Block block in repo.GetBlocks(new[] { chain.Genesis }.Concat(chain.EnumerateAfter(chain.Genesis)).Where(c => c.Height % BlockGranularity == 0).Select(c => c.HashBlock), default(CancellationToken)))
            {
                cumul += block.Transactions.Count * BlockGranularity;
                blockCount += BlockGranularity;
                if (cumul <= TransactionsPerWork)
                {
                    continue;
                }

                ChainedBlock nextFrom = chain.GetBlock(chain.GetBlock(block.GetHash()).Height + BlockGranularity);
                if (nextFrom == null)
                {
                    break;
                }

                EnqueueRange(chain, @from, blockCount);
                @from = nextFrom;
                blockCount = 0;
                cumul = 0;
            }

            blockCount = (chain.Tip.Height - from.Height) + 1;
            EnqueueRange(chain, from, blockCount);

            byte[] bytes = chain.Tip.GetLocator().ToBytes();
            blobLock.UploadText(
                Encoders.Hex.EncodeData(bytes),
                null,
                new AccessCondition { LeaseId = lease });
        }

        private void EnqueueRange(ChainBase chain, ChainedBlock startCumul, int blockCount)
        {
            ListenerTrace.Info("Enqueing from " + startCumul.Height + " " + blockCount + " blocks");
            if (blockCount == 0)
            {
                return;
            }

            Task<bool>[] tasks = _IndexTasks
                .Where(t => chain.FindFork(t.Value.Item1.BlockLocator).Height <= startCumul.Height + blockCount)
                .Select(t => new BlockRange
                {
                    From = startCumul.Height,
                    Count = blockCount,
                    Target = t.Key
                })
                .Select(t => _Conf.Topics.InitialIndexing.AddAsync(t))
                .ToArray();

            try
            {
                Task.WaitAll(tasks);
            }
            catch (AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException ?? aex).Throw();
                throw;
            }
        }
    }
}
