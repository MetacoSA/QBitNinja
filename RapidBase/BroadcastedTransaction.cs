using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase
{
    public class BroadcastedTransaction
    {
        public BroadcastedTransaction()
        {

        }
        public BroadcastedTransaction(DynamicTableEntity entity)
        {
            var bytes = entity.Properties["d"].BinaryValue;
            Transaction = new Transaction(bytes);
        }
        public Transaction Transaction
        {
            get;
            set;
        }
        public DynamicTableEntity ToEntity()
        {
            var entity = new DynamicTableEntity("a", Transaction.GetHash().ToString());
            entity.Properties.Add("d", new EntityProperty(Transaction.ToBytes()));
            entity.RowKey = Transaction.GetHash().ToString();
            entity.PartitionKey = entity.RowKey;
            return entity;
        }
    }
}
