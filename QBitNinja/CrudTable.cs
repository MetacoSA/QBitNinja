using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin.Indexer;

namespace QBitNinja
{
    public class CrudTableFactory
    {
        private readonly Func<CloudTable> _createTable;
        public Scope Scope { get; }

        public CrudTableFactory(Func<CloudTable> createTable, Scope scope = null)
        {
            _createTable = createTable ?? throw new ArgumentNullException("createTable");
            Scope = scope ?? new Scope();
        }

        public CrudTable<T> GetTable<T>(string tableName)
        {
            CloudTable table = _createTable();
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
            Table = table ?? throw new ArgumentNullException("table");
            Scope = new Scope();
        }

        public Scope Scope { get; set; }

        public CloudTable Table { get; }

        public async Task<bool> CreateAsync(string itemId, T item, bool orReplace = true)
        {
            try
            {
                string callbackStr = Serializer.ToString(item);
                DynamicTableEntity entity = new DynamicTableEntity(Escape(Scope), Escape(itemId))
                {
                    Properties =
                    {
                        new KeyValuePair<string, EntityProperty>("data", new EntityProperty(callbackStr))
                    }
                };
                await Table.ExecuteAsync(orReplace ? TableOperation.InsertOrReplace(entity) : TableOperation.Insert(entity)).ConfigureAwait(false);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation != null
                    && ex.RequestInformation.HttpStatusCode == 409
                    && !orReplace)
                {
                    return false;
                }

                throw;
            }

            return true;
        }

        public bool Create(string itemId, T item, bool orReplace = true)
        {
            try
            {
                return CreateAsync(itemId, item, orReplace).Result;
            }
            catch (AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException ?? aex).Throw();
                throw;
            }
        }

        public void Delete()
        {
            foreach (DynamicTableEntity child in Table.ExecuteQuery(AllInScope(Scope)))
            {
                Ignore(404, () => Table.Execute(TableOperation.Delete(child)));
            }
        }

        private void Ignore(int errorCode, Action act)
        {
            try
            {
                act();
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation == null
                    || ex.RequestInformation.HttpStatusCode != errorCode)
                {
                    throw;
                }
            }
        }

        public bool Delete(string itemId, bool includeChildren = false)
        {
            try
            {
                Table.Execute(TableOperation.Delete(new DynamicTableEntity(Escape(Scope), Escape(itemId))
                {
                    ETag = "*"
                }));

                if (includeChildren)
                {
                    Scope children = Scope.GetChild(itemId);
                    foreach (DynamicTableEntity child in Table.ExecuteQuery(AllInScope(children)))
                    {
                        Ignore(404, () => Table.Execute(TableOperation.Delete(child)));
                    }
                }
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation != null
                    && ex.RequestInformation.HttpStatusCode == 404)
                {
                    return false;
                }

                throw;
            }

            return true;
        }

        private static TableQuery AllInScope(Scope children)
        {
            return new TableQuery
            {
                FilterString = TableQuery.GenerateFilterCondition(
                    "PartitionKey",
                    QueryComparisons.Equal,
                    Escape(children))
            };
        }

        public T[] Read()
        {
            return Table.ExecuteQuery(new TableQuery
            {
                FilterString = TableQuery.GenerateFilterCondition(
                    "PartitionKey",
                    QueryComparisons.Equal,
                    Escape(Scope))
            })
            .Select(e => Serializer.ToObject<T>(e.Properties["data"].StringValue))
            .ToArray();
        }

        private static string Escape(Scope scope)
        {
            return Escape(scope.ToString());
        }

        private static string Escape(string key)
        {
            return FastEncoder.Instance.EncodeData(Encoding.UTF8.GetBytes(key));
        }



        public async Task<T> ReadOneAsync(string item)
        {
            DynamicTableEntity e = (await Table.ExecuteAsync(TableOperation.Retrieve(Escape(Scope), Escape(item))).ConfigureAwait(false)).Result as DynamicTableEntity;
            return e == null
                ? default(T)
                : Serializer.ToObject<T>(e.Properties["data"].StringValue);
        }


        public T ReadOne(string item)
        {
            try
            {
                return ReadOneAsync(item).Result;
            }
            catch (AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException ?? aex).Throw();
                throw;
            }
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
