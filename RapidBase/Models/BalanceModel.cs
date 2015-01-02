using NBitcoin;
using NBitcoin.Indexer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase.Models
{
    public class BalanceModel
    {
        public BalanceModel()
        {

        }
        public BalanceModel(IEnumerable<OrderedBalanceChange> balance, ConcurrentChain chain)
        {
            Operations = balance
                .Select(c => new BalanceOperation(c, chain))
                .ToList();
            Total = Operations.Select(c => c.Change).Sum();
        }
        public Money Total
        {
            get;
            set;
        }

        public List<BalanceOperation> Operations
        {
            get;
            set;
        }

        public bool IsComplete
        {
            get;
            set;
        }
    }

    public class BalanceOperation
    {

        public BalanceOperation()
        {
            ReceivedCoins = new List<Coin>();
            SpentCoins = new List<Coin>();
        }

        public BalanceOperation(OrderedBalanceChange balanceChange, ChainBase chain)
        {
            ReceivedCoins = balanceChange.ReceivedCoins.ToList();
            SpentCoins = balanceChange.SpentCoins.ToList();
            Change = balanceChange.Amount;
            TransactionId = balanceChange.TransactionId;

            if (balanceChange.BlockId != null)
            {
                BlockId = balanceChange.BlockId;
                Confirmations = (chain.Tip.Height - chain.GetBlock(balanceChange.BlockId).Height) + 1;
            }
        }
        public Money Change
        {
            get;
            set;
        }

        public int Confirmations
        {
            get;
            set;
        }

        public uint256 BlockId
        {
            get;
            set;
        }

        public uint256 TransactionId
        {
            get;
            set;
        }

        public List<Coin> ReceivedCoins
        {
            get;
            set;
        }
        public List<Coin> SpentCoins
        {
            get;
            set;
        }
        public override string ToString()
        {
            return Change.ToString();
        }
    }
}
