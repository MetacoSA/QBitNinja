using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase
{
    public class CallbackRepository
    {
        public CallbackRepository(RapidBaseConfiguration configuration)
        {
            Configuration = configuration;
        }
        public RapidBaseConfiguration Configuration
        {
            get;
            set;
        }
        public CallbackRegistration CreateCallback(string eventName, CallbackRegistration callback)
        {
            callback.Id = null;
            var callbackStr = Serializer.ToString(callback);
            var id = Hash(callbackStr);
            var table = Configuration.GetCallbackTable();
            table.Execute(TableOperation.InsertOrReplace(new DynamicTableEntity(eventName, id)
            {
                Properties =
                {
                    new KeyValuePair<string,EntityProperty>("data",new EntityProperty(callbackStr))
                }
            }));
            var result = Serializer.ToObject<CallbackRegistration>(callbackStr);
            result.Id = id;
            return result;
        }

        private string Hash(string data)
        {
            return Hashes.Hash256(Encoding.UTF8.GetBytes(data)).ToString();
        }

        public CallbackRegistration[] GetCallbacks(string eventName)
        {
            var table = Configuration.GetCallbackTable();
            return table.ExecuteQuery(new TableQuery()
            {
                FilterString = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, eventName)
            })
            .Select(e => Parse(e))
            .ToArray();
        }

        private CallbackRegistration Parse(DynamicTableEntity entity)
        {
            var registration = Serializer.ToObject<CallbackRegistration>(entity.Properties["data"].StringValue);
            registration.Id = entity.RowKey;
            return registration;
        }

        public bool Delete(string eventName, string id)
        {
            var table = Configuration.GetCallbackTable();
            try
            {

                table.Execute(TableOperation.Delete(new DynamicTableEntity(eventName, id)
                {
                    ETag = "*"
                }));
                return true;
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == 404)
                    return false;
                throw;
            }
        }
    }
}
