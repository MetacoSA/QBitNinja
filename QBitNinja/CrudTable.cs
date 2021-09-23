﻿using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.Indexer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja
{
    public class CrudTableFactory
    {
        public CrudTableFactory(JsonSerializerSettings serializerSettings, Func<CloudTable> createTable, Scope scope = null)
        {
            if (serializerSettings == null)
                throw new ArgumentNullException(nameof(serializerSettings));
            if (createTable == null)
                throw new ArgumentNullException("createTable");
            if (scope == null)
                scope = new Scope();
            _createTable = createTable;
            Scope = scope;
            this.serializerSettings = serializerSettings;
        }

        readonly Func<CloudTable> _createTable;

        public Scope Scope
        {
            get;
            private set;
        }

        private readonly JsonSerializerSettings serializerSettings;

        public CrudTable<T> GetTable<T>(string tableName)
        {
            var table = _createTable();
            return new CrudTable<T>(table, serializerSettings)
            {
                Scope = Scope.GetChild(tableName)
            };
        }
    }
    
    /// <summary>
    /// Wraps an Azure CloudTable, offering CRUD operations for objects of a specific type, automatically
    /// serializing to and from JSON when querying the CloudTable.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CrudTable<T>
    {
        public CrudTable(CloudTable table, JsonSerializerSettings serializerSettings)
        {
            if (table == null)
                throw new ArgumentNullException("table");
            if (serializerSettings == null)
                throw new ArgumentNullException(nameof(serializerSettings));
            Table = table;
            Scope = new Scope();
            this.serializerSettings = serializerSettings;
        }

        public Scope Scope
        {
            get;
            set;
        }

        private readonly JsonSerializerSettings serializerSettings;
        
        public CloudTable Table
        {
            get;
            private set;
        }

        public async Task<bool> CreateAsync(string itemId, T item, bool orReplace = true)
        {
            try
            {
                var callbackStr = JsonConvert.SerializeObject(item, serializerSettings);
                var entity = new DynamicTableEntity(Escape(Scope), Escape(itemId))
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
                if (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == 409 && !orReplace)
                    return false;
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
                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
                throw;
            }
        }

        public void Delete()
        {
            foreach (var child in Table.ExecuteQuery(AllInScope(Scope)))
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
                if (ex.RequestInformation == null || ex.RequestInformation.HttpStatusCode != errorCode)
                    throw;
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
                    var childScope = Scope.GetChild(itemId);  // 'itemId' can be a  path.
                    foreach (var child in Table.ExecuteQuery(AllInScope(childScope)))
                    {
                        Ignore(404, () => Table.Execute(TableOperation.Delete(child)));
                    }
                }
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == 404)
                    return false;
                throw;
            }
            return true;
        }

        private static TableQuery AllInScope(QBitNinja.Scope scope)
        {
            return new TableQuery()
            {
                FilterString = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, Escape(scope))
            };
        }

        public T[] Read()
        {
            return Table.ExecuteQuery(new TableQuery
            {
                FilterString = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, Escape(Scope))
            })
            .Select(e => JsonConvert.DeserializeObject<T>(e.Properties["data"].StringValue, serializerSettings))
            .ToArray();
        }

        private static string Escape(Scope scope)
        {
            return Escape(scope.ToString());
        }

        private static string Escape(string key)
        {
            var result = FastEncoder.Instance.EncodeData(Encoding.UTF8.GetBytes(key));
            return result;
        }

        public async Task<T> ReadOneAsync(string item)
        {
            var e = (await Table.ExecuteAsync(TableOperation.Retrieve(Escape(Scope), Escape(item))).ConfigureAwait(false)).Result as DynamicTableEntity;
            if (e == null)
                return default(T);
            return JsonConvert.DeserializeObject<T>(e.Properties["data"].StringValue, serializerSettings);
        }

        public T ReadOne(string item)
        {
            try
            {
                return ReadOneAsync(item).Result;
            }
            catch (AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
                throw;
            }
        }

        /// <summary>
        /// Get a CrudTable that is scoped to some nested path.
        /// </summary>
        public CrudTable<T> GetChild(params string[] children)
        {
            return new CrudTable<T>(Table, serializerSettings)
            {
                Scope = Scope.GetChild(children)
            };
        }
    }
}
