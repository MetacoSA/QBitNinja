using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.Indexer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase
{
    /// <summary>
    /// Such table can store data keyed by Height/BlockId/TransactionId, and range query them 
    /// </summary>
    public class ChainTable<T>
    {
        CloudTable _CloudTable;
        public ChainTable(CloudTable cloudTable)
        {
            if (cloudTable == null)
                throw new ArgumentNullException("cloudTable");
            _CloudTable = cloudTable;
        }

        public CloudTable Table
        {
            get
            {
                return _CloudTable;
            }
        }

        public string Scope
        {
            get;
            set;
        }

        public void Create(BalanceLocator locator, T item)
        {
            var str = Serializer.ToString(item);
            var entity = new DynamicTableEntity(Escape(Scope), Escape(locator))
            {
                Properties =
                {
                    new KeyValuePair<string,EntityProperty>("data",new EntityProperty(str))
                }
            };
            Table.Execute(TableOperation.InsertOrReplace(entity));
        }

        public IEnumerable<T> Query(ChainBase chain, BalanceQuery query = null)
        {
            if (query == null)
                query = new BalanceQuery();
            var tableQuery = query.CreateTableQuery(Escape(Scope), "");
            return ExecuteBalanceQuery(Table, tableQuery, query.PageSizes)
                   .Where(_ => chain.Contains(UnEscapeLocator(_.RowKey).BlockHash))
                   .Select(_ => Serializer.ToObject<T>(_.Properties["data"].StringValue));
        }


        private IEnumerable<DynamicTableEntity> ExecuteBalanceQuery(CloudTable table, TableQuery tableQuery, IEnumerable<int> pages)
        {
            pages = pages ?? new int[0];
            var pagesEnumerator = pages.GetEnumerator();
            TableContinuationToken continuation = null;
            do
            {
                tableQuery.TakeCount = pagesEnumerator.MoveNext() ? (int?)pagesEnumerator.Current : null;
                var segment = table.ExecuteQuerySegmented(tableQuery, continuation);
                continuation = segment.ContinuationToken;
                foreach (var entity in segment)
                {
                    yield return entity;
                }
            } while (continuation != null);
        }

        private string Escape(BalanceLocator locator)
        {
            locator = Normalize(locator);
            return "-" + locator.ToString(true);
        }
        private BalanceLocator UnEscapeLocator(string str)
        {
            return BalanceLocator.Parse(str.Substring(1), true);
        }


        private static BalanceLocator Normalize(BalanceLocator locator)
        {
            locator = new BalanceLocator(locator.Height, locator.BlockHash ?? new uint256(0), locator.TransactionId ?? new uint256(0));
            return locator;
        }

        private string Escape(string scope)
        {
            var result = FastEncoder.Instance.EncodeData(Encoding.UTF8.GetBytes(scope));
            return result;
        }
    }
}
