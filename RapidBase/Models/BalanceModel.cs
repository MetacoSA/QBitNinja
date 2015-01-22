using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
#if !CLIENT
using NBitcoin.Indexer;
#endif
namespace RapidBase.Models
{
    public class BalanceModel
    {
        public BalanceModel()
        {

        }
#if !CLIENT
        public BalanceModel(IEnumerable<OrderedBalanceChange> balance, ConcurrentChain chain)
        {
            Operations = balance
                .Where(b => b.SpentCoins.Count > 0 || b.ReceivedCoins.Count > 0)
                .Select(c => new BalanceOperation(c, chain))
                .ToList();
        }

        public BalanceLocator Continuation
        {
            get;
            set;
        }
#else
        public string Continuation
        {
            get;
            set;
        }
#endif
        public List<BalanceOperation> Operations
        {
            get;
            set;
        }
    }


    public class BalanceSummaryDetails
    {
        public BalanceSummaryDetails()
        {
            Received = Money.Zero;
            Amount = Money.Zero;
        }
        public int TransactionCount
        {
            get;
            set;
        }
        public Money Amount
        {
            get;
            set;
        }
        public Money Received
        {
            get;
            set;
        }

        public static BalanceSummaryDetails operator +(BalanceSummaryDetails c1, BalanceSummaryDetails c2)
        {
            if (c1 == null)
                return c2;
            if (c2 == null)
                return c1;
            return new BalanceSummaryDetails
            {
                Amount = c1.Amount + c2.Amount,
                TransactionCount = c1.TransactionCount + c2.TransactionCount,
                Received = c1.Received + c2.Received
            };
        }
        public static BalanceSummaryDetails operator -(BalanceSummaryDetails c1, BalanceSummaryDetails c2)
        {
            return c1 + (-c2);
        }

        public static BalanceSummaryDetails operator -(BalanceSummaryDetails c1)
        {
            if (c1 == null)
                return null;
            BalanceSummaryDetails result = new BalanceSummaryDetails
            {
                Amount = -c1.Amount, 
                Received = -c1.Received, 
                TransactionCount = -c1.TransactionCount
            };
            return result;
        }
#if !CLIENT
        internal static BalanceSummaryDetails CreateFrom(List<OrderedBalanceChange> changes)
        {
            return new BalanceSummaryDetails
            {
                Amount = changes.Select(_ => _.Amount).Sum(),
                TransactionCount = changes.Count,
                Received = changes.Select(_ => _.Amount < Money.Zero ? Money.Zero : _.Amount).Sum(),
            };
        }
#endif
    }

    public enum CacheHit
    {
        NoCache,
        PartialCache,
        FullCache
    }
    public class BalanceSummary
    {
        public BalanceSummary()
        {
            Confirmed = new BalanceSummaryDetails();
        }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public BalanceSummaryDetails UnConfirmed
        {
            get;
            set;
        }

        public BalanceSummaryDetails Confirmed
        {
            get;
            set;
        }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public BalanceSummaryDetails Spendable
        {
            get;
            set;
        }
        public BalanceSummaryDetails Immature
        {
            get;
            set;
        }

        public void CalculateSpendable()
        {
            Spendable = UnConfirmed + (Confirmed - Immature);
        }
#if !CLIENT
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public BalanceLocator Locator
        {
            get;
            set;
        }
#endif

        internal void PrepareForSend(BlockFeature at, bool debug)
        {
            if (at != null)
            {
                UnConfirmed = null;
            }
            CalculateSpendable();
#if !CLIENT
            Locator = null;
#endif
            if (!debug)
            {
                CacheHit = null;
            }
            OlderImmature = 0;
        }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int OlderImmature
        {
            get;
            set;
        }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public CacheHit? CacheHit
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
#if !CLIENT
        public BalanceOperation(OrderedBalanceChange balanceChange, ChainBase chain)
        {
            ReceivedCoins = balanceChange.ReceivedCoins.ToList();
            SpentCoins = balanceChange.SpentCoins.ToList();
            Amount = balanceChange.Amount;
            TransactionId = balanceChange.TransactionId;

            if (balanceChange.BlockId != null)
            {
                BlockId = balanceChange.BlockId;
                Height = chain.GetBlock(balanceChange.BlockId).Height;
                Confirmations = (chain.Tip.Height - Height) + 1;
            }
        }
#endif
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
        public int Height
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
