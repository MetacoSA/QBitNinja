using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin.Indexer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase
{
    public class CrudTable<T>
    {
        public CrudTable(CloudTable table)
        {
            if (table == null)
                throw new ArgumentNullException("table");
            _Table = table;
        }

        private readonly CloudTable _Table;
        public CloudTable Table
        {
            get
            {
                return _Table;
            }
        }

        public void Create(string collection, string itemId, T item)
        {
            var callbackStr = Serializer.ToString(item);
            Table.Execute(TableOperation.InsertOrReplace(new DynamicTableEntity(Escape(collection), Escape(itemId))
            {
                Properties =
                {
                    new KeyValuePair<string,EntityProperty>("data",new EntityProperty(callbackStr))
                }
            }));
        }
        public T[] Read(string collection)
        {
            return Table.ExecuteQuery(new TableQuery()
            {
                FilterString = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, Escape(collection))
            })
            .Select(e => Serializer.ToObject<T>(e.Properties["data"].StringValue))
            .ToArray();
        }

        private string Escape(string collection)
        {
            return FastEncoder.Instance.EncodeData(Encoding.UTF8.GetBytes(collection));
        }

        public bool Delete(string collection, string item)
        {
            try
            {

                Table.Execute(TableOperation.Delete(new DynamicTableEntity(Escape(collection), Escape(item))
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
