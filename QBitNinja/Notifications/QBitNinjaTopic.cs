using Microsoft.Data.OData;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QBitNinja.Notifications;
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

namespace QBitNinja.Notifications
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
    
    public class QBitNinjaTopic<T> : QBitNinjaQueueBase<T, TopicCreation, TopicDescription>
        where T : class
    {
        public QBitNinjaTopic(string connectionString, string topic)
            : this(connectionString, new TopicCreation()
            {
                Path = topic
            })
        {

        }
        public QBitNinjaTopic(string connectionString, TopicCreation topic, SubscriptionCreation defaultSubscription = null)
            : base(connectionString, topic)
        {
            _Subscription = defaultSubscription;
            if (_Subscription == null)
                _Subscription = new SubscriptionCreation()
                {
                    Name = GetMac()
                };
            _Subscription.TopicPath = topic.Path;
        }


        private readonly SubscriptionCreation _Subscription;
        public SubscriptionCreation Subscription
        {
            get
            {
                return _Subscription;
            }
        }


        internal TopicClient CreateTopicClient()
        {
            var client = TopicClient.CreateFromConnectionString(ConnectionString, Topic);
            return client;
        }
        public QBitNinjaTopic<T> CreateConsumer(string subscriptionName = null)
        {
            return CreateConsumer(new SubscriptionCreation()
            {
                Name = subscriptionName,
            });
        }


        public QBitNinjaTopic<T> CreateConsumer(SubscriptionCreation subscriptionDescription)
        {
            if (subscriptionDescription == null)
                throw new ArgumentNullException("subscriptionDescription");
            if (subscriptionDescription.Name == null)
                subscriptionDescription.Name = GetMac();
            subscriptionDescription.TopicPath = Creation.Path;
            subscriptionDescription.Merge(Subscription);
            return new QBitNinjaTopic<T>(ConnectionString, Creation, subscriptionDescription);
        }

        private string GetMac()
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            PhysicalAddress address = nics[0].GetPhysicalAddress();
            byte[] bytes = address.GetAddressBytes();
            return Encoders.Hex.EncodeData(bytes);
        }

        public string Topic
        {
            get
            {
                return Creation.Path;
            }
        }
        public SubscriptionClient CreateSubscriptionClient()
        {
            var client = SubscriptionClient.CreateFromConnectionString(ConnectionString, Creation.Path, Subscription.Name, ReceiveMode.ReceiveAndDelete);
            return client;
        }
        public override Task EnsureSetupAsync()
        {
            return GetNamespace().EnsureTopicExistAsync(Creation);
        }

        protected override Task SendAsync(BrokeredMessage brokered)
        {
            return CreateTopicClient().SendAsync(brokered);
        }

        protected override IDisposable OnMessageCore(Action<BrokeredMessage> act, OnMessageOptions options)
        {
            var client = CreateSubscriptionClient();
            client.OnMessage(act, options);
            return new ActionDisposable(() => client.Close());
        }

        protected override Task<BrokeredMessage> ReceiveAsyncCore(TimeSpan timeout)
        {
            return CreateSubscriptionClient().ReceiveAsync(timeout);
        }

        public async Task EnsureExistsAndDrainedAsync()
        {
            var subscription = await GetNamespace().EnsureSubscriptionExistsAsync(Subscription).ConfigureAwait(false);
            await DrainMessagesAsync().ConfigureAwait(false);
        }

        public QBitNinjaTopic<T> EnsureSubscriptionExists()
        {
            try
            {

                GetNamespace().EnsureSubscriptionExistsAsync(Subscription).Wait();
            }
            catch (AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
            }
            return this;
        }
    }
}