using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Notifications
{
    public class BroadcastedTransaction
    {
        public BroadcastedTransaction()
        {

        }
        public BroadcastedTransaction(Transaction tx)
        {
            Transaction = tx;
        }
        public int Tried
        {
            get;
            set;
        }
        public Transaction Transaction
        {
            get;
            set;
        }
    }
}
