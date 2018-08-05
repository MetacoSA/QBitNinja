using NBitcoin;

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
