using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.Indexer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QBitNinja
{
    /// <summary>
    /// Such table can store data keyed by Height/BlockId/TransactionId, and range query them 
    /// </summary>
    public class ChainTable<T>
    {
        readonly CloudTable _cloudTable;
        public ChainTable(CloudTable cloudTable)
        {
            if(cloudTable == null)
                throw new ArgumentNullException("cloudTable");
            _cloudTable = cloudTable;
        }

        public CloudTable Table
        {
            get
            {
                return _cloudTable;
            }
        }

        public Scope Scope
        {
            get;
            set;
        }

        public void Create(ConfirmedBalanceLocator locator, T item)
        {
            var str = Serializer.ToString(item);
            var entity = new DynamicTableEntity(Escape(Scope), Escape(locator));
            PutData(entity, str);
            Table.Execute(TableOperation.InsertOrReplace(entity));
        }



        public void Delete(ConfirmedBalanceLocator locator)
        {
            var entity = new DynamicTableEntity(Escape(Scope), Escape(locator))
            {
                ETag = "*"
            };
            Table.Execute(TableOperation.Delete(entity));
        }
        public void Delete()
        {
            foreach(var entity in Table.ExecuteQuery(new TableQuery()
            {
                FilterString = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, Escape(Scope))
            }))
            {
                Table.Execute(TableOperation.Delete(entity));
            }
        }

        public IEnumerable<T> Query(ChainBase chain, BalanceQuery query = null)
        {
            if(query == null)
                query = new BalanceQuery();
            var tableQuery = query.CreateTableQuery(Escape(Scope), "");
            return ExecuteBalanceQuery(Table, tableQuery, query.PageSizes)
                   .Where(_ => chain.Contains(((ConfirmedBalanceLocator)UnEscapeLocator(_.RowKey)).BlockHash))
                   .Select(_ => Serializer.ToObject<T>(ParseData(_)));
        }

        private string ParseData(DynamicTableEntity entity)
        {
            int i = 0;
            StringBuilder builder = new StringBuilder();
            while(true)
            {
                string name = i == 0 ? "data" : "data" + i;
                if(!entity.Properties.ContainsKey(name))
                    break;
                builder.Append(entity.Properties[name].StringValue);
                i++;
            }
            return builder.ToString();
        }
        private void PutData(DynamicTableEntity entity, string str)
        {
            int i = 0;
            foreach(var part in Split(str, 30000))
            {
                string name = i == 0 ? "data" : "data" + i;
                entity.Properties.Add(name, new EntityProperty(part));
                i++;
            }
        }

        private IEnumerable<string> Split(string str, int charCount)
        {
            int index = 0;
            while(index != str.Length)
            {
                var count = Math.Min(charCount, str.Length - index);
                yield return str.Substring(index, count);
                index += count;
            }
        }

        private IEnumerable<DynamicTableEntity> ExecuteBalanceQuery(CloudTable table, TableQuery<DynamicTableEntity> tableQuery, IEnumerable<int> pages)
        {
            pages = pages ?? new int[0];
            var pagesEnumerator = pages.GetEnumerator();
            TableContinuationToken continuation = null;
            do
            {
                tableQuery.TakeCount = pagesEnumerator.MoveNext() ? (int?)pagesEnumerator.Current : null;
                var segment = table.ExecuteQuerySegmented(tableQuery, continuation);
                continuation = segment.ContinuationToken;
                foreach(var entity in segment)
                {
                    yield return entity;
                }
            } while(continuation != null);
        }

        private static string Escape(ConfirmedBalanceLocator locator)
        {
            locator = Normalize(locator);
            return "-" + locator.ToString(true);
        }

        private static BalanceLocator UnEscapeLocator(string str)
        {
            return BalanceLocator.Parse(str.Substring(1), true);
        }

        private static ConfirmedBalanceLocator Normalize(ConfirmedBalanceLocator locator)
        {
            locator = new ConfirmedBalanceLocator(locator.Height, locator.BlockHash ?? new uint256(0), locator.TransactionId ?? new uint256(0));
            return locator;
        }

        private static string Escape(string scope)
        {
            var result = FastEncoder.Instance.EncodeData(Encoding.UTF8.GetBytes(scope));
            return result;
        }

        private static string Escape(Scope scope)
        {
            return Escape(scope.ToString());
        }
    }
}
