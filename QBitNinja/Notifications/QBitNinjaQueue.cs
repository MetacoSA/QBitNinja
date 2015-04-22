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
    public class QBitNinjaQueue<T>
    {
        public QBitNinjaQueue(string connectionString, string topic)
            : this(connectionString, new QueueCreation()
            {
                Path = topic
            })
        {

        }
        public QBitNinjaQueue(string connectionString, QueueCreation queue, SubscriptionCreation defaultSubscription = null)
        {
            _Queue = queue;
            _DefaultSubscription = defaultSubscription;
            _ConnectionString = connectionString;
        }

        SubscriptionCreation _DefaultSubscription;

        public async Task<bool> AddAsync(T entity)
        {
            var client = CreateQueueClient();
            var str = Serializer.ToString<T>(entity);
            BrokeredMessage brokered = new BrokeredMessage(str);
            if (_Queue.RequiresDuplicateDetection.HasValue &&
                _Queue.RequiresDuplicateDetection.Value)
            {
                if (GetMessageId == null)
                    throw new InvalidOperationException("Requires Duplicate Detection is on, but the callback GetMessageId is not set");
                brokered.MessageId = GetMessageId(entity);
            }
            await client.SendAsync(brokered).ConfigureAwait(false);
            return true;
        }

        internal QueueClient CreateQueueClient()
        {
            var client = QueueClient.CreateFromConnectionString(ConnectionString, Queue);
            return client;
        }

        public Func<T, string> GetMessageId
        {
            get;
            set;
        }


        public IDisposable OnMessage(Action<T> evt)
        {
            return OnMessage((a, b) => evt(a));
        }
        public IDisposable OnMessage(Action<T, MessageControl> evt)
        {
            var client = CreateQueueClient();
            client.OnMessage(bm =>
            {
                var control = new MessageControl();
                var obj = ToObject(bm);
                if (obj == null)
                    return;
                evt(obj, control);
                if (control._Scheduled != null)
                {
                    BrokeredMessage message = new BrokeredMessage(Serializer.ToString(obj));
                    message.MessageId = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
                    message.ScheduledEnqueueTimeUtc = control._Scheduled.Value;
                    CreateQueueClient().Send(message);
                }
            }, new OnMessageOptions()
            {
                AutoComplete = true,
                MaxConcurrentCalls = 1
            });
            return new ActionDisposable(() => client.Close());
        }

        private static T ToObject(BrokeredMessage bm)
        {
            if (bm == null)
                return default(T);
            var result = bm.GetBody<string>();
            var obj = Serializer.ToObject<T>(result);
            return obj;
        }


        private readonly QueueCreation _Queue;
        public string Queue
        {
            get
            {
                return _Queue.Path;
            }
        }
        private readonly string _ConnectionString;
        public string ConnectionString
        {
            get
            {
                return _ConnectionString;
            }
        }



        internal Task EnsureSetupAsync()
        {
            return GetNamespace().EnsureQueueExistAsync(_Queue);
        }

        public NamespaceManager GetNamespace()
        {
            return NamespaceManager.CreateFromConnectionString(ConnectionString);
        }
    }
}
