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
            var client = QueueClient.CreateFromConnectionString(ConnectionString, Queue);
            return client;
        }





        public string Queue
        {
            get
            {
                return Creation.Path;
            }
        }

        public override Task EnsureSetupAsync()
        {
            return GetNamespace().EnsureQueueExistAsync(Creation);
        }

        protected override Task SendAsync(BrokeredMessage brokered)
        {
            return CreateQueueClient().SendAsync(brokered);
        }

        protected override IDisposable OnMessageCore(Action<BrokeredMessage> act, OnMessageOptions options)
        {
            var client = CreateQueueClient();
            client.OnMessage(act, options);
            return new ActionDisposable(() => client.Close());
        }

        protected override Task<BrokeredMessage> ReceiveAsyncCore(TimeSpan timeout)
        {
            return CreateQueueClient().ReceiveAsync(timeout);
        }
    }
}
