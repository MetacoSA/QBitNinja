using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Notifications
{
    public class QBitNinjaQueue<T> : QBitNinjaQueueBase<T, QueueCreation, QueueDescription>
    {
        public QBitNinjaQueue(string connectionString, string topic)
            : this(connectionString, new QueueCreation()
            {
                Path = topic
            })
        {

        }
        public QBitNinjaQueue(string connectionString, QueueCreation queue)
            : base(connectionString, queue)
        {
        }


        internal QueueClient CreateQueueClient()
        {
            var client = QueueClient.CreateFromConnectionString(ConnectionString, Queue, ReceiveMode.PeekLock);
            return client;
        }





        public string Queue
        {
            get
            {
                return Creation.Path;
            }
        }


        protected override async Task SendAsync(BrokeredMessage brokered)
        {
            var queue = CreateQueueClient();
            try
            {
                await queue.SendAsync(brokered).ConfigureAwait(false);
            }
            finally
            {
                var unused = queue.CloseAsync();
            }
        }

        protected override IDisposable OnMessageAsyncCore(Func<BrokeredMessage, Task> act, OnMessageOptions options)
        {
            var client = CreateQueueClient();
            client.OnMessageAsync(async (bm) =>
            {
                try
                {
                    await act(bm).ConfigureAwait(false);
                }
                catch(Exception ex)
                {
                    OnUnHandledException(ex);
                }
            }, options);
            return new ActionDisposable(() => client.Close());
        }

        protected override Task<BrokeredMessage> ReceiveAsyncCore(TimeSpan timeout)
        {
            return CreateQueueClient().ReceiveAsync(timeout);
        }

        protected override Task DeleteAsync()
        {
            return GetNamespace().DeleteQueueAsync(Queue);
        }

        protected override Task<QueueDescription> CreateAsync(QueueDescription description)
        {
            return GetNamespace().CreateQueueAsync(description);
        }

        protected override Task<QueueDescription> GetAsync()
        {
            return GetNamespace().GetQueueAsync(Queue);
        }

        protected override void InitDescription(QueueDescription description)
        {
            description.Path = Creation.Path;
        }

        protected override bool Validate(QueueCreation other)
        {
            return Creation.Validate(other);
        }
    }
}
