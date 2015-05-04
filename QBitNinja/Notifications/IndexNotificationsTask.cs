using NBitcoin.Indexer;
using NBitcoin.Indexer.IndexTasks;
using QBitNinja.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Notifications
{
    public class IndexNotificationsTask : IndexTask<Notify>
    {
        private SubscriptionCollection _Subscriptions;
        private QBitNinjaConfiguration _Conf;
        public IndexNotificationsTask(QBitNinjaConfiguration conf, SubscriptionCollection subscriptions)
            : base(conf.Indexer)
        {
            _Subscriptions = subscriptions;
            _Conf = conf;
        }
        protected override Task EnsureSetup()
        {
            return Task.FromResult(true);
        }

        protected override void IndexCore(string partitionName, IEnumerable<Notify> items)
        {
            _Conf
                .Topics
                .SendNotifications
                .AddAsync(items.First()).Wait();
        }

        protected override int PartitionSize
        {
            get
            {
                return 1;
            }
        }

        protected override bool SkipToEnd
        {
            get
            {
                return _Subscriptions.Count == 0;
            }
        }

        protected override void ProcessBlock(NBitcoin.Indexer.BlockInfo block, BulkImport<Notify> bulk)
        {
            foreach (var newBlock in _Subscriptions.GetNewBlocks())
            {
                bulk.Add("o", new Notify(new Notification()
                {
                    Subscription = newBlock,
                    Data = new NewBlockNotificationData()
                    {
                        Header = block.Block.Header,
                        BlockId = block.BlockId,
                        Height = block.Height
                    }
                }));
            }
        }
    }
}
