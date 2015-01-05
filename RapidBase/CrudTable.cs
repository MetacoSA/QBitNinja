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

        public string Scope
        {
            get;
            set;
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

        private string Escape(string collection, bool scoped = true)
        {
            var result = FastEncoder.Instance.EncodeData(Encoding.UTF8.GetBytes(collection));
            if (Scope != null && scoped)
                result = Escape(Scope, false) + result;
            return result;
        }

        public void Delete(string collection, string item)
        {
            Table.Execute(TableOperation.Delete(new DynamicTableEntity(Escape(collection), Escape(item))
            {
                ETag = "*"
            }));
        }
    }
}
