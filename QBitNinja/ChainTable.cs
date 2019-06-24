using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.Indexer;

namespace QBitNinja
{
    /// <summary>
    /// Such table can store data keyed by Height/BlockId/TransactionId, and range query them 
    /// </summary>
    public class ChainTable<T>
    {
        public ChainTable(CloudTable cloudTable)
        {
            Table = cloudTable ?? throw new ArgumentNullException("cloudTable");
        }

        public CloudTable Table { get; }

        public Scope Scope
        {
            get;
            set;
        }

        public void Create(ConfirmedBalanceLocator locator, T item)
        {
            string str = Serializer.ToString(item);
            DynamicTableEntity entity = new DynamicTableEntity(Escape(Scope), Escape(locator));
            PutData(entity, str);
            Table.Execute(TableOperation.InsertOrReplace(entity));
        }



        public void Delete(ConfirmedBalanceLocator locator)
        {
            DynamicTableEntity entity = new DynamicTableEntity(Escape(Scope), Escape(locator))
            {
                ETag = "*"
            };
            Table.Execute(TableOperation.Delete(entity));
        }

        public void Delete()
        {
            IEnumerable<DynamicTableEntity> queryResult = Table.ExecuteQuery(new TableQuery
            {
                FilterString = TableQuery.GenerateFilterCondition(
                    "PartitionKey",
                    QueryComparisons.Equal,
                    Escape(Scope))
            });

            foreach (DynamicTableEntity entity in queryResult)
            {
                Table.Execute(TableOperation.Delete(entity));
            }
        }

        public IEnumerable<T> Query(ChainBase chain, BalanceQuery query = null)
        {
            query = query ?? new BalanceQuery();
            TableQuery<DynamicTableEntity> tableQuery = query.CreateTableQuery(Escape(Scope), string.Empty);
            return ExecuteBalanceQuery(Table, tableQuery, query.PageSizes)
                   .Where(_ => chain.Contains(((ConfirmedBalanceLocator)UnEscapeLocator(_.RowKey)).BlockHash))
                   .Select(_ => Serializer.ToObject<T>(ParseData(_)));
        }

        private string ParseData(DynamicTableEntity entity)
        {
            var index = 0;
            StringBuilder builder = new StringBuilder();
            while (true)
            {
                string name = index == 0 ? "data" : "data" + index;
                if (!entity.Properties.ContainsKey(name))
                {
                    break;
                }

                builder.Append(entity.Properties[name].StringValue);
                index++;
            }

            return builder.ToString();
        }

        private void PutData(DynamicTableEntity entity, string str)
        {
            var i = 0;
            foreach (string part in Split(str, 30000))
            {
                string name = i == 0 ? "data" : "data" + i;
                entity.Properties.Add(name, new EntityProperty(part));
                i++;
            }
        }

        private IEnumerable<string> Split(string str, int charCount)
        {
            var index = 0;
            while (index != str.Length)
            {
                int count = Math.Min(charCount, str.Length - index);
                yield return str.Substring(index, count);
                index += count;
            }
        }

        private IEnumerable<DynamicTableEntity> ExecuteBalanceQuery(
            CloudTable table,
            TableQuery<DynamicTableEntity> tableQuery,
            IEnumerable<int> pages)
        {
            pages = pages ?? new int[0];
            using (IEnumerator<int> pagesEnumerator = pages.GetEnumerator())
            {
                TableContinuationToken continuation = null;
                do
                {
                    tableQuery.TakeCount = pagesEnumerator.MoveNext() ? pagesEnumerator.Current : (int?)null;
                    TableQuerySegment<DynamicTableEntity> segment = table.ExecuteQuerySegmented(
                        tableQuery,
                        continuation);
                    continuation = segment.ContinuationToken;
                    foreach (DynamicTableEntity entity in segment)
                    {
                        yield return entity;
                    }
                }
                while (continuation != null);
            }
        }

        private static string Escape(ConfirmedBalanceLocator locator)
        {
            return "-" + Normalize(locator).ToString(true);
        }

        private static BalanceLocator UnEscapeLocator(string str)
        {
            return BalanceLocator.Parse(str.Substring(1), true);
        }

        private static ConfirmedBalanceLocator Normalize(ConfirmedBalanceLocator locator)
        {
            return new ConfirmedBalanceLocator(
                locator.Height,
                locator.BlockHash ?? new uint256(0),
                locator.TransactionId ?? new uint256(0));
        }

        private static string Escape(string scope)
        {
            return FastEncoder.Instance.EncodeData(Encoding.UTF8.GetBytes(scope));
        }

        private static string Escape(Scope scope)
        {
            return Escape(scope.ToString());
        }
    }
}
