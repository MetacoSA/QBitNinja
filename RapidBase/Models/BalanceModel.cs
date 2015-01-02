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
        public BalanceModel(BalanceSheet balance, ConcurrentChain chain)
        {
            Operations = balance
                .All
                .WhereNotExpired()
                .Select(c => new BalanceOperation(c))
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

        public BalanceOperation(OrderedBalanceChange balanceChange)
        {
            ReceivedCoins = balanceChange.ReceivedCoins.ToList();
            SpentCoins = balanceChange.SpentCoins.ToList();
            Change = balanceChange.Amount;
        }
        public Money Change
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
