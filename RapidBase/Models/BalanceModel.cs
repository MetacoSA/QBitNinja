using NBitcoin;
using NBitcoin.Indexer;
using System.Collections.Generic;
using System.Linq;

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
                .Where(b => b.SpentCoins.Count > 0 || b.ReceivedCoins.Count > 0)
                .Select(c => new BalanceOperation(c, chain))
                .ToList();
            PageTotal = Operations.Select(o => o.Amount).Sum();
            Total = PageTotal;
        }

        public Money Total
        {
            get;
            set;
        }
        public Money PageTotal
        {
            get;
            set;
        }

        public BalanceLocator Continuation
        {
            get;
            set;
        }

        public List<BalanceOperation> Operations
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
            Amount = balanceChange.Amount;
            TransactionId = balanceChange.TransactionId;

            if (balanceChange.BlockId != null)
            {
                BlockId = balanceChange.BlockId;
                Confirmations = (chain.Tip.Height - chain.GetBlock(balanceChange.BlockId).Height) + 1;
            }
        }
        public Money Amount
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
            return Amount.ToString();
        }
    }
}
