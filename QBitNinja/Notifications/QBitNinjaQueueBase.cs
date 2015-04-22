using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Notifications
{
    public abstract class QBitNinjaQueueBase<T, TCreation, TDescription> where TCreation : ICreation
    {
        public QBitNinjaQueueBase(string connectionString, TCreation creation)
        {
            _Creation = creation;
            _ConnectionString = connectionString;
        }

        protected abstract Task SendAsync(BrokeredMessage brokered);
        protected abstract IDisposable OnMessageCore(Action<BrokeredMessage> act, OnMessageOptions options);
        
        protected abstract Task<BrokeredMessage> ReceiveAsyncCore(TimeSpan timeout);

        private readonly string _ConnectionString;
        public string ConnectionString
        {
            get
            {
                return _ConnectionString;
            }
        }

        private readonly TCreation _Creation;
        public TCreation Creation
        {
            get
            {
                return _Creation;
            }
        }
        public async Task<bool> AddAsync(T entity)
        {
            var str = Serializer.ToString<T>(entity);
            BrokeredMessage brokered = new BrokeredMessage(str);
            if (Creation.RequiresDuplicateDetection.HasValue &&
                Creation.RequiresDuplicateDetection.Value)
            {
                if (GetMessageId == null)
                    throw new InvalidOperationException("Requires Duplicate Detection is on, but the callback GetMessageId is not set");
                brokered.MessageId = GetMessageId(entity);
            }
            await SendAsync(brokered).ConfigureAwait(false);
            return true;
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
            return OnMessageCore(bm =>
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
                    Send(message);
                }
            }, new OnMessageOptions()
            {
                AutoComplete = true,
                MaxConcurrentCalls = 1
            });
        }

        public void Send(BrokeredMessage message)
        {
            try
            {
                SendAsync(message).Wait();
            }
            catch (AggregateException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        public async Task DrainMessagesAsync()
        {
            while (await ReceiveAsync().ConfigureAwait(false) != null)
            {
            }
        }        

        public async Task<T> ReceiveAsync(TimeSpan? timeout = null)
        {
            if (timeout == null)
                timeout = TimeSpan.Zero;
            BrokeredMessage message = null;
            message = await ReceiveAsyncCore(timeout.Value).ConfigureAwait(false);
            return ToObject(message);
        }

        


        private static T ToObject(BrokeredMessage bm)
        {
            if (bm == null)
                return default(T);
            var result = bm.GetBody<string>();
            var obj = Serializer.ToObject<T>(result);
            return obj;
        }

        public NamespaceManager GetNamespace()
        {
            return NamespaceManager.CreateFromConnectionString(ConnectionString);
        }


        public async Task<TDescription> EnsureExistsAsync()
        {
            var create = ChangeType<TCreation, TDescription>(Creation);
            InitDescription(create);

            TDescription result = default(TDescription);
            try
            {
                result = await GetAsync().ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {
            }
            if (result == null)
            {
                result = await CreateAsync(create).ConfigureAwait(false);
            }
            if (!Validate(ChangeType<TDescription,TCreation>(result)))
            {
                await DeleteAsync().ConfigureAwait(false);
                return await EnsureExistsAsync().ConfigureAwait(false);
            }
            return result;
        }

        protected abstract Task DeleteAsync();

        protected abstract Task<TDescription> CreateAsync(TDescription description);
        protected abstract Task<TDescription> GetAsync();

        protected abstract void InitDescription(TDescription description);

        protected abstract bool Validate(TCreation other);

        private static T2 ChangeType<T1, T2>(T1 input)
        {
            MemoryStream ms = new MemoryStream();
            DataContractSerializer seria = new DataContractSerializer(typeof(T1));
            seria.WriteObject(ms, input);
            ms.Position = 0;
            seria = new DataContractSerializer(typeof(T2));
            return (T2)seria.ReadObject(ms);
        }
    }
}
