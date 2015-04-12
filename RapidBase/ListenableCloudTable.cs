using Microsoft.Data.OData;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
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
    public class CloudTablePublisher
    {
        private ListenableCloudTable _Parent;

        internal CloudTablePublisher(ListenableCloudTable parent)
        {
            this._Parent = parent;
        }

        public async Task<bool> AddAsync(DynamicTableEntity entity)
        {
            if (_Parent.CloudTable != null)
            {
                try
                {
                    await _Parent.CloudTable.ExecuteAsync(TableOperation.Insert(entity)).ConfigureAwait(false);
                }
                catch (StorageException ex)
                {
                    if (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == 409)
                        return false;
                    throw;
                }
            }
            var client = CreateTopicClient();
            var message = ListenableCloudTable.FromTableEntity(entity);
            message.Properties.Add("Addition", "true");
            message.PartitionKey = entity.PartitionKey;
            message.Properties["RowKey"] = entity.RowKey;
            await client.SendAsync(message).ConfigureAwait(false);
            return true;
        }

        private TopicClient CreateTopicClient()
        {
            var client = TopicClient.CreateFromConnectionString(_Parent.ConnectionString, _Parent.Topic);
            return client;
        }
        public async Task RemoveAsync(DynamicTableEntity entity)
        {
            if (_Parent.CloudTable != null)
            {
                await _Parent.CloudTable.ExecuteAsync(TableOperation.Delete(entity)).ConfigureAwait(false);
            }
            var client = CreateTopicClient();
            var message = new BrokeredMessage();
            message.Properties.Add("Addition", "false");
            message.Properties.Add("RowKey", entity.RowKey);
            message.PartitionKey = entity.PartitionKey;
            await client.SendAsync(message).ConfigureAwait(false);
        }
    }
    public class CloudTableConsumer
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
        private ListenableCloudTable _Parent;
        private string subscriptionName;

        internal CloudTableConsumer(ListenableCloudTable parent, string subscriptionName)
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

        public async Task<CloudTableEvent> ReceiveAsync(TimeSpan? timeout = null)
        {
            if (timeout == null)
                timeout = TimeSpan.Zero;
            var client = CreateSubscriptionClient();
            BrokeredMessage message = null;
            message = await client.ReceiveAsync(timeout.Value).ConfigureAwait(false);
            return ToCloudTableEvent(message);
        }

        private static CloudTableEvent ToCloudTableEvent(BrokeredMessage message)
        {
            if (message == null)
                return null;
            CloudTableEvent evt = new CloudTableEvent();
            evt.Addition = message.Properties["Addition"].ToString() == "true";
            if (evt.Addition)
                evt.AddedEntity = ListenableCloudTable.ToTableEntity(message);
            evt.PartitionKey = evt.PartitionKey;
            evt.RowKey = message.Properties["RowKey"].ToString();
            return evt;
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

        public CloudTableConsumer EnsureExists()
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

        public IDisposable OnMessage(Action<CloudTableEvent> evt)
        {
            var client = CreateSubscriptionClient();
            client.OnMessage(bm =>
            {
                evt(ToCloudTableEvent(bm));
            }, new OnMessageOptions()
            {
                AutoComplete = true,
                MaxConcurrentCalls = 1
            });
            if (_Parent.CloudTable != null)
            {
                foreach (var ev in _Parent.CloudTable
                    .ExecuteQuery(new TableQuery())
                    .Select(e => new CloudTableEvent()
                    {
                        Addition = true,
                        AddedEntity = e,
                        PartitionKey = e.PartitionKey,
                        RowKey = e.RowKey
                    }))
                {
                    evt(ev);
                }
            }
            return new SubscriptionClientDisposable(client);
        }

    }

    public class CloudTableEvent
    {
        public bool Addition
        {
            get;
            set;
        }
        public bool Deletion
        {
            get
            {
                return !Addition;
            }
        }

        public DynamicTableEntity AddedEntity
        {
            get;
            set;
        }

        public string PartitionKey
        {
            get;
            set;
        }
        public string RowKey
        {
            get;
            set;
        }
    }

    public class ListenableCloudTable
    {
        public ListenableCloudTable(CloudTable cloudTable, string connectionString, string topic)
        {
            _Topic = topic;
            _ConnectionString = connectionString;
            _CloudTable = cloudTable;
        }

        public CloudTablePublisher CreatePublisher()
        {
            return new CloudTablePublisher(this);
        }

        public CloudTableConsumer CreateConsumer(string subscriptionName)
        {
            return new CloudTableConsumer(this, subscriptionName);
        }


        public CloudTableConsumer CreateConsumer()
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

        private readonly CloudTable _CloudTable;
        public CloudTable CloudTable
        {
            get
            {
                return _CloudTable;
            }
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

        public static DynamicTableEntity ToTableEntity(BrokeredMessage message)
        {
            DynamicTableEntity entity = new DynamicTableEntity(message.PartitionKey, message.Properties["RowKey"].ToString());
            var json = JObject.Parse(message.GetBody<string>());
            foreach (var property in json.Properties())
            {
                if (property.Name == "Etag")
                    entity.ETag = property.Value.ToObject<string>();
                else if (property.Name == "Timestamp")
                    entity.Timestamp = property.Value.ToObject<DateTimeOffset>();
                else
                    Deserizalize(json, property, entity);
            }
            return entity;
        }

        public static BrokeredMessage FromTableEntity(DynamicTableEntity entity)
        {
            JObject json = new JObject();
            foreach (var property in entity.Properties)
            {
                Serialize(json, property.Key, property.Value);
            }
            json.Add(new JProperty("Etag", entity.ETag));
            json.Add(new JProperty("Timestamp", entity.Timestamp));

            var message = new BrokeredMessage(json.ToString(Formatting.None));
            message.PartitionKey = entity.PartitionKey;
            message.Properties["RowKey"] = entity.RowKey;
            return message;
        }

        private static void Serialize(JObject json, string key, EntityProperty property)
        {
            JProperty jprop = new JProperty(key);
            json.Add(jprop);
            var jobj = new JObject();
            jprop.Value = jobj;
            jobj.Add(new JProperty("type", property.PropertyType.ToString()));
            jobj.Add(new JProperty("value", Serialize(property)));
        }

        private static object Serialize(EntityProperty property)
        {
            if (property.PropertyAsObject == null)
                return null;

            switch (property.PropertyType)
            {
                case EdmType.Binary:
                    return Convert.ToBase64String((byte[])property.PropertyAsObject);
                case EdmType.Boolean:
                case EdmType.DateTime:
                case EdmType.Guid:
                case EdmType.Double:
                case EdmType.Int32:
                case EdmType.Int64:
                case EdmType.String:
                    return new JValue(property.PropertyAsObject);
            }
            throw new NotSupportedException();
        }
        private static void Deserizalize(JObject json, JProperty property, DynamicTableEntity entity)
        {
            var name = property.Name;
            var jobj = (JObject)property.Value;
            var entityProperty = Deserialize(jobj);
            entity.Properties.Add(name, entityProperty);
        }

        private static EntityProperty Deserialize(JObject jobj)
        {
            var edmType = (EdmType)Enum.Parse(typeof(EdmType), jobj["type"].ToString());

            var value = ((JValue)jobj["value"]).Value;
            switch (edmType)
            {
                case EdmType.Binary:
                    return new EntityProperty(value == null ? null as byte[] : Convert.FromBase64String(value.ToString()));
                case EdmType.Boolean:
                    return new EntityProperty(value as bool?);
                case EdmType.DateTime:
                    return new EntityProperty(value as DateTimeOffset?);
                case EdmType.Guid:
                    return new EntityProperty(value as Guid?);
                case EdmType.Double:
                    return new EntityProperty(value as Double?);
                case EdmType.Int32:
                    return new EntityProperty(value as int?);
                case EdmType.Int64:
                    return new EntityProperty(value as long?);
                case EdmType.String:
                    return new EntityProperty(value as string);
            }
            return null;
        }

        internal Task EnsureSetupAsync()
        {
            List<Task> tasks = new List<Task>();
            if (CloudTable != null)
            {
                tasks.Add(CloudTable.CreateIfNotExistsAsync());
            }
            tasks.Add(GetNamespace().EnsureTopicExistAsync(Topic));
            return Task.WhenAll(tasks.ToArray());
        }

        public NamespaceManager GetNamespace()
        {
            return NamespaceManager.CreateFromConnectionString(ConnectionString);
        }
    }
}