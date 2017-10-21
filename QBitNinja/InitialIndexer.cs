using Microsoft.WindowsAzure.Storage;
using Microsoft.Extensions.Logging;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QBitNinja
{
    public class BlockRange
    {
        public string Target
        {
            get;
            set;
        }
        public int From
        {
            get;
            set;
        }
        public int Count
        {
            get;
            set;
        }
        public bool Processed
        {
            get;
            set;
        }
        public override string ToString()
        {
            return Target + "- " + From + "-" + Count;
        }
    }


    public class InitialIndexer
    {

        QBitNinjaConfiguration _Conf;
        public InitialIndexer(QBitNinjaConfiguration conf)
        {
            if (conf == null)
                throw new ArgumentNullException("conf");
            _Conf = conf;
            BlockGranularity = 100;
            TransactionsPerWork = 1000 * 1000;
            Init();
        }

        public int BlockGranularity
        {
            get;
            set;
        }
        public int TransactionsPerWork
        {
            get;
            set;
        }



        private void Init()
        {
            var indexer = _Conf.Indexer.CreateIndexer();
            AddTaskIndex(indexer.GetCheckpoint(IndexerCheckpoints.Balances), new IndexBalanceTask(_Conf.Indexer, null));
            AddTaskIndex(indexer.GetCheckpoint(IndexerCheckpoints.Blocks), new IndexBlocksTask(_Conf.Indexer));
            AddTaskIndex(indexer.GetCheckpoint(IndexerCheckpoints.Transactions), new IndexTransactionsTask(_Conf.Indexer));
        }

        Dictionary<string, Tuple<Checkpoint, IIndexTask>> _IndexTasks = new Dictionary<string, Tuple<Checkpoint, IIndexTask>>();

        void AddTaskIndex(Checkpoint checkpoint, IIndexTask indexTask)
        {
            _IndexTasks.Add(checkpoint.CheckpointName, Tuple.Create(checkpoint, indexTask));
        }


        public void Cancel()
        {
            var blobLock = GetInitBlob();
            try
            {
                try
                {
                    blobLock.DeleteAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    blobLock.BreakLeaseAsync(null).GetAwaiter().GetResult();
                    blobLock.DeleteIfExistsAsync().GetAwaiter().GetResult();
                }
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation == null || ex.RequestInformation.HttpStatusCode != 404)
                    throw;
            }
            Logs.Main.LogInformation("Init blob deleted");
            Logs.Main.LogInformation("Deleting queue...");
			_Conf.GetInitialIndexingQueued().Delete();
			_Conf.GetInitialIndexingProcessing().Delete();
            Logs.Main.LogInformation("Queue deleted");
        }

        public int Run(ChainBase chain = null)
        {
            Logs.Main.LogInformation("Start initial indexing");
            int totalProcessed = 0;
            using (var node = _Conf.Indexer.ConnectToNode(false))
            {

                Logs.Main.LogInformation("Handshaking...");
                node.VersionHandshake();
                Logs.Main.LogInformation("Handshaked");
                chain = chain ?? node.GetChain();
                Logs.Main.LogInformation("Current chain at height " + chain.Height);
                var blockRepository = new NodeBlocksRepository(node);

                var blobLock = GetInitBlob();
                string lease = null;
                try
                {
                    blobLock.UploadTextAsync("Enqueuing").GetAwaiter().GetResult();
                    lease = blobLock.AcquireLeaseAsync(null, null).GetAwaiter().GetResult();
                }
                catch (StorageException)
                {

                }
                if (lease != null)
                {
                    Logs.Main.LogInformation("Queueing index jobs");
                    EnqueueJobs(blockRepository, chain, blobLock, lease);
                }
                Logs.Main.LogInformation("Dequeuing index jobs");

                while (true)
                {
                    var msg = _Conf.Topics
                       .InitialIndexing
                       .ReceiveAsync(TimeSpan.FromMilliseconds(1000))
                       .Result;

                    var ns = _Conf.Topics.InitialIndexing.GetNamespace();
                    var description = ns.GetQueue(_Conf.Topics.InitialIndexing.Queue);

                    Console.WriteLine("Work remaining in the queue : " + description.MessageCountDetails.ActiveMessageCount);
                    if (msg == null)
                    {
                        var state = blobLock.DownloadTextAsync().GetAwaiter().GetResult();
                        if (state == "Enqueuing" || description.MessageCountDetails.ActiveMessageCount != 0)
                        {
                            Logs.Main.LogInformation("Additional work will be enqueued...");
                            continue;
                        }
                        else
                        {
                            var locator = new BlockLocator();
                            locator.FromBytes(Encoders.Hex.DecodeData(state));
                            UpdateCheckpoints(locator);
                            break;
                        }
                    }

                    using (msg.Message)
                    {
                        var range = msg.Body;
                        using(var sched = new CustomThreadPoolTaskScheduler(50, 100, range.ToString()))
                        {

                            Logs.Main.LogInformation("Processing " + range.ToString());
                            totalProcessed++;
                            var task = _IndexTasks[range.Target];
                            BlockFetcher fetcher = new BlockFetcher(task.Item1, blockRepository, chain)
                            {
                                FromHeight = range.From,
                                ToHeight = range.From + range.Count - 1
                            };
                            try
                            {
                                task.Item2.SaveProgression = false;
                                task.Item2.EnsureIsSetup = totalProcessed == 0;
                                var index = Task.Factory.StartNew(() =>
                                {
                                    task.Item2.Index(fetcher, sched);
                                }, TaskCreationOptions.LongRunning);
                                while(!index.Wait(TimeSpan.FromMinutes(4)))
                                {
                                    msg.Message.RenewLock();
                                    Logs.Main.LogInformation("Lock renewed");
                                }
                            }
                            catch(AggregateException aex)
                            {
                                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
                                throw;
                            }

                            range.Processed = true;
                            msg.Message.Complete();
                        }
                    }
                }
            }
            Logs.Main.LogInformation("Initial indexing terminated");
            return totalProcessed;
        }

        private CloudBlockBlob GetInitBlob()
        {
            var container = _Conf.Indexer.GetBlocksContainer();
            var blobLock = container.GetBlockBlobReference("initialindexer/lock");
            return blobLock;
        }

        private void UpdateCheckpoints(BlockLocator locator)
        {
            Logs.Main.LogInformation("Work finished, updating checkpoints");
            foreach (var chk in _IndexTasks.Select(kv => kv.Value.Item1))
            {
                Logs.Main.LogInformation(chk.CheckpointName + "...");
                chk.SaveProgress(locator);
            }
            Logs.Main.LogInformation("Checkpoints updated");
        }

        private void EnqueueJobs(NodeBlocksRepository repo, ChainBase chain, CloudBlockBlob blobLock, string lease)
        {
            int cumul = 0;
            ChainedBlock from = chain.Genesis;
            int blockCount = 0;
            foreach (var block in repo.GetBlocks(new[] { chain.Genesis }.Concat(chain.EnumerateAfter(chain.Genesis)).Where(c => c.Height % BlockGranularity == 0).Select(c => c.HashBlock), default(CancellationToken)))
            {
                cumul += block.Transactions.Count * BlockGranularity;
                blockCount += BlockGranularity;
                if (cumul > TransactionsPerWork)
                {
                    var nextFrom = chain.GetBlock(chain.GetBlock(block.GetHash()).Height + BlockGranularity);
                    if (nextFrom == null)
                        break;
                    EnqueueRange(chain, from, blockCount);
                    from = nextFrom;
                    blockCount = 0;
                    cumul = 0;
                }
            }

            blockCount = (chain.Tip.Height - from.Height) + 1;
            EnqueueRange(chain, from, blockCount);

            var bytes = chain.Tip.GetLocator().ToBytes();
            blobLock.UploadTextAsync(Encoders.Hex.EncodeData(bytes), null, new AccessCondition()
            {
                LeaseId = lease
            }, null, null).GetAwaiter().GetResult();
        }

        private void EnqueueRange(ChainBase chain, ChainedBlock startCumul, int blockCount)
        {
            Logs.Main.LogInformation("Enqueing from " + startCumul.Height + " " + blockCount + " blocks");
            if (blockCount == 0)
                return;
            var tasks = _IndexTasks
                .Where(t => chain.FindFork(t.Value.Item1.BlockLocator).Height <= startCumul.Height + blockCount)
                .Select(t => new BlockRange()
                {
                    From = startCumul.Height,
                    Count = blockCount,
                    Target = t.Key
                })
                .Select(t => _Conf.GetInitialIndexingQueued().CreateAsync(t.ToString(), t, true))
                .ToArray();

            try
            {
                Task.WaitAll(tasks);
            }
            catch (AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
                throw;
            }
        }

    }
}
