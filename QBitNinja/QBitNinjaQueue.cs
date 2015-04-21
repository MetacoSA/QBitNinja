using Microsoft.Data.OData;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace QBitNinja
{

    public class MessageControl
    {

        internal DateTime? _Scheduled;
        public MessageControl()
        {
        }
        public void RescheduleIn(TimeSpan delta)
        {
            RescheduleFor(DateTime.UtcNow + delta);
        }

        public void RescheduleFor(DateTime date)
        {
            date = date.ToUniversalTime();
            _Scheduled = date;
        }
    }
    public class QBitNinjaQueueConsumer<T> where T : class
    {
        class SubscriptionClientDisposable : IDisposable
        {
            SubscriptionClient _Client;
            public SubscriptionClientDisposable(SubscriptionClient client)
            {
                _Client = client;
            }
            #region IDisposable Members

            public void Dispose()
            {
                if (!_Client.IsClosed)
                    _Client.Close();
            }

            #endregion
        }
        private QBitNinjaQueue<T> _Parent;
        private string subscriptionName;

        internal QBitNinjaQueueConsumer(QBitNinjaQueue<T> parent, string subscriptionName)
        {
            this._Parent = parent;
            this.subscriptionName = subscriptionName;
        }

        public async Task DrainMessages()
        {
            while (await ReceiveAsync().ConfigureAwait(false) != null)
            {
            }
        }

        public async Task<T> ReceiveAsync(TimeSpan? timeout = null)
        {
            if (timeout == null)
                timeout = TimeSpan.Zero;
            var client = CreateSubscriptionClient();
            BrokeredMessage message = null;
            message = await client.ReceiveAsync(timeout.Value).ConfigureAwait(false);
            return ToObject(message);
        }

        public SubscriptionClient CreateSubscriptionClient()
        {
            var client = SubscriptionClient.CreateFromConnectionString(_Parent.ConnectionString, _Parent.Topic, subscriptionName, ReceiveMode.ReceiveAndDelete);
            return client;
        }

        public async Task EnsureExistsAndDrainedAsync()
        {
            var subscription = await _Parent.GetNamespace().EnsureSubscriptionExistsAsync(_Parent.Topic, subscriptionName).ConfigureAwait(false);
            await DrainMessages().ConfigureAwait(false);
        }

        public QBitNinjaQueueConsumer<T> EnsureExists()
        {
            try
            {

                _Parent.GetNamespace().EnsureSubscriptionExistsAsync(_Parent.Topic, subscriptionName).Wait();
            }
            catch (AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
            }
            return this;
        }

        public IDisposable OnMessage(Action<T> evt)
        {
            return OnMessage((a, b) => evt(a));
        }
        public IDisposable OnMessage(Action<T, MessageControl> evt)
        {
            var client = CreateSubscriptionClient();
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
                    _Parent.CreateTopicClient().Send(message);
                }
            }, new OnMessageOptions()
            {
                AutoComplete = true,
                MaxConcurrentCalls = 1
            });
            return new SubscriptionClientDisposable(client);
        }

        private static T ToObject(BrokeredMessage bm)
        {
            if (bm == null)
                return default(T);
            var result = bm.GetBody<string>();
            var obj = Serializer.ToObject<T>(result);
            return obj;
        }

    }

    public class QBitNinjaQueue<T> where T : class
    {
        public QBitNinjaQueue(string connectionString, string topic)
        {
            _Topic = topic;
            _ConnectionString = connectionString;
        }

        public async Task<bool> AddAsync(T entity)
        {
            var client = CreateTopicClient();
            var str = Serializer.ToString<T>(entity);
            BrokeredMessage brokered = new BrokeredMessage(str);
            await client.SendAsync(brokered).ConfigureAwait(false);
            return true;
        }

        internal TopicClient CreateTopicClient()
        {
            var client = TopicClient.CreateFromConnectionString(ConnectionString, Topic);
            return client;
        }
        public QBitNinjaQueueConsumer<T> CreateConsumer(string subscriptionName)
        {
            return new QBitNinjaQueueConsumer<T>(this, subscriptionName);
        }


        public QBitNinjaQueueConsumer<T> CreateConsumer()
        {
            return CreateConsumer(GetMac());
        }

        private string GetMac()
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            PhysicalAddress address = nics[0].GetPhysicalAddress();
            byte[] bytes = address.GetAddressBytes();
            return Encoders.Hex.EncodeData(bytes);
        }

        private readonly string _Topic;
        public string Topic
        {
            get
            {
                return _Topic;
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
            List<Task> tasks = new List<Task>();
            tasks.Add(GetNamespace().EnsureTopicExistAsync(Topic));
            return Task.WhenAll(tasks.ToArray());
        }

        public NamespaceManager GetNamespace()
        {
            return NamespaceManager.CreateFromConnectionString(ConnectionString);
        }
    }
}