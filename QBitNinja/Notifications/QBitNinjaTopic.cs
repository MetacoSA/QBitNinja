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


        public void Complete()
        {
            if(Options.AutoComplete)
                throw new InvalidOperationException("Options.AutoComplete should be true for calling this method");
            _Complete = true;
        }
        internal bool _Complete;


        public OnMessageOptions Options
        {
            get;
            set;
        }
    }

    public class QBitNinjaTopic<T> : QBitNinjaQueueBase<T, TopicCreation, TopicDescription>
        where T : class
    {

        class QBitNinjaTopicSubscription : QBitNinjaQueueBase<T, SubscriptionCreation, SubscriptionDescription>
        {
            private TopicCreation _Topic;
            public QBitNinjaTopicSubscription(string connectionString, TopicCreation topic, SubscriptionCreation subscription)
                : base(connectionString, subscription)
            {
                _Topic = topic;
            }
            protected override Task SendAsync(BrokeredMessage brokered)
            {
                throw new NotSupportedException();
            }

            protected override IDisposable OnMessageAsyncCore(Func<BrokeredMessage, Task> act, OnMessageOptions options)
            {
                var client = CreateSubscriptionClient();
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
                return new ActionDisposable(() => client.CloseAsync());
            }

            public SubscriptionClient CreateSubscriptionClient()
            {
                var client = SubscriptionClient.CreateFromConnectionString(ConnectionString, _Topic.Path, Creation.Name, ReceiveMode.ReceiveAndDelete);
                return client;
            }
            internal TopicClient CreateTopicClient()
            {
                var client = TopicClient.CreateFromConnectionString(ConnectionString, _Topic.Path);
                return client;
            }

            protected override Task<BrokeredMessage> ReceiveAsyncCore(TimeSpan timeout)
            {
                return CreateSubscriptionClient().ReceiveAsync(timeout);
            }

            protected override Task DeleteAsync()
            {
                return GetNamespace().DeleteSubscriptionAsync(_Topic.Path, Creation.Name);
            }

            protected override Task<SubscriptionDescription> CreateAsync(SubscriptionDescription description)
            {
                return GetNamespace().CreateSubscriptionAsync(description);
            }

            protected override Task<SubscriptionDescription> GetAsync()
            {
                return GetNamespace().GetSubscriptionAsync(Creation.TopicPath, Creation.Name);
            }

            protected override void InitDescription(SubscriptionDescription description)
            {
                description.TopicPath = Creation.TopicPath;
                description.Name = Creation.Name;
            }

            protected override bool Validate(SubscriptionCreation other)
            {
                return Creation.Validate(other);
            }

            internal Task<BrokeredMessage> ReceiveAsyncCorePublic(TimeSpan timeout)
            {
                return ReceiveAsyncCore(timeout);
            }

            internal IDisposable OnMessageCoreAsyncPublic(Func<BrokeredMessage, Task> act, OnMessageOptions options)
            {
                return OnMessageAsyncCore(act, options);
            }
        }

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
            if(_Subscription == null)
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



        public QBitNinjaTopic<T> CreateConsumer(string subscriptionName, bool machineScope)
        {
            return CreateConsumer(new SubscriptionCreation()
            {
                Name = machineScope ? GetMac() + subscriptionName : subscriptionName,
            });
        }


        public QBitNinjaTopic<T> CreateConsumer(SubscriptionCreation subscriptionDescription)
        {
            if(subscriptionDescription == null)
                throw new ArgumentNullException("subscriptionDescription");
            if(subscriptionDescription.Name == null)
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

        internal TopicClient CreateTopicClient()
        {
            var client = TopicClient.CreateFromConnectionString(ConnectionString, Topic);
            return client;
        }

        protected override async Task SendAsync(BrokeredMessage brokered)
        {
            var topic = CreateTopicClient();
            try
            {
                await topic.SendAsync(brokered).ConfigureAwait(false);
                return;
            }
            finally
            {
                var unsued = topic.CloseAsync();
            }
        }

        QBitNinjaTopicSubscription CreateSubscriptionClient()
        {
            return new QBitNinjaTopicSubscription(ConnectionString, Creation, Subscription);
        }

        protected override IDisposable OnMessageAsyncCore(Func<BrokeredMessage, Task> act, OnMessageOptions options)
        {
            return CreateSubscriptionClient().OnMessageCoreAsyncPublic(act, options);
        }

        protected override Task<BrokeredMessage> ReceiveAsyncCore(TimeSpan timeout)
        {
            return CreateSubscriptionClient().ReceiveAsyncCorePublic(timeout);
        }

        public override async Task DrainMessagesAsync()
        {
            var delete = (await GetNamespace().GetSubscriptionsAsync(Topic).ConfigureAwait(false))
                .Select(s => GetNamespace().DeleteSubscriptionAsync(Topic, s.Name)).ToArray();
            await Task.WhenAll(delete).ConfigureAwait(false);
        }

        public Task EnsureSubscriptionExistsAsync()
        {
            return CreateSubscriptionClient().EnsureExistsAsync();
        }

        public QBitNinjaTopic<T> EnsureSubscriptionExists()
        {
            try
            {
                CreateSubscriptionClient().EnsureExistsAsync().Wait();
            }
            catch(AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
            }
            return this;
        }

        protected override Task DeleteAsync()
        {
            return GetNamespace().DeleteTopicAsync(Topic);
        }

        protected override Task<TopicDescription> CreateAsync(TopicDescription description)
        {
            return GetNamespace().CreateTopicAsync(description);
        }

        protected override Task<TopicDescription> GetAsync()
        {
            return GetNamespace().GetTopicAsync(Topic);
        }

        protected override void InitDescription(TopicDescription description)
        {
            description.Path = Topic;
        }

        protected override bool Validate(TopicCreation other)
        {
            return Creation.Validate(other);
        }
    }
}