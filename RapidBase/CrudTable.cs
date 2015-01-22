using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin.Indexer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RapidBase
{
    public class CrudTableFactory
    {
        public CrudTableFactory(Func<CloudTable> createTable, Scope scope = null)
        {
            if (createTable == null)
                throw new ArgumentNullException("createTable");
            if (scope == null)
                scope = new Scope();
            _CreateTable = createTable;
            Scope = scope;
        }

        Func<CloudTable> _CreateTable;
        public Scope Scope
        {
            get;
            private set;
        }

        public CrudTable<T> GetTable<T>(string tableName)
        {
            var table = _CreateTable();
            return new CrudTable<T>(table)
            {
                Scope = Scope.GetChild(tableName)
            };
        }
    }
    public class CrudTable<T>
    {
        public CrudTable(CloudTable table)
        {
            if (table == null)
                throw new ArgumentNullException("table");
            _table = table;
            Scope = new Scope();
        }

        public Scope Scope
        {
            get;
            set;
        }
        private readonly CloudTable _table;
        public CloudTable Table
        {
            get
            {
                return _table;
            }
        }

        public void Create(string itemId, T item)
        {
            var callbackStr = Serializer.ToString(item);
            Table.Execute(TableOperation.InsertOrReplace(new DynamicTableEntity(Escape(Scope), Escape(itemId))
            {
                Properties =
                {
                    new KeyValuePair<string,EntityProperty>("data",new EntityProperty(callbackStr))
                }
            }));
        }

        public T[] Read()
        {
            return Table.ExecuteQuery(new TableQuery
            {
                FilterString = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, Escape(Scope))
            })
            .Select(e => Serializer.ToObject<T>(e.Properties["data"].StringValue))
            .ToArray();
        }

        private string Escape(Scope scope)
        {
            return Escape(scope.ToString());
        }

        private string Escape(string key)
        {
            var result = FastEncoder.Instance.EncodeData(Encoding.UTF8.GetBytes(key));
            return result;
        }

        public void Delete(string collection, string item)
        {
            Table.Execute(TableOperation.Delete(new DynamicTableEntity(Escape(collection), Escape(item))
            {
                ETag = "*"
            }));
        }

        public T ReadOne(string item)
        {
            var e = Table.Execute(TableOperation.Retrieve(Escape(Scope), Escape(item))).Result as DynamicTableEntity;
            if (e == null)
                throw new StorageException(new RequestResult()
                {
                    HttpStatusCode = 404
                }, "Item not found", null);
            return Serializer.ToObject<T>(e.Properties["data"].StringValue);
        }

        public CrudTable<T> GetChild(params string[] children)
        {
            return new CrudTable<T>(Table)
            {
                Scope = Scope.GetChild(children)
            };
        }
    }
}
