using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using NBitcoin.OpenAsset;
using System;

#if !CLIENT
using NBitcoin.Indexer;
#endif
#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
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

    public class AssetBalanceSummaryDetails
    {
        public BitcoinAssetId Asset
        {
            get;
            set;
        }
        public long Quantity
        {
            get;
            set;
        }
        public long Received
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

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public AssetBalanceSummaryDetails[] Assets
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
                Received = c1.Received + c2.Received,
                Assets = Add(c1.Assets, c2.Assets)
            };
        }

        private static AssetBalanceSummaryDetails[] Add(AssetBalanceSummaryDetails[] a, AssetBalanceSummaryDetails[] b)
        {
            if (a == null && b == null)
                return null;
            if (a != null && b == null)
                return a;
            if (a == null && b != null)
                return b;
            List<AssetBalanceSummaryDetails> result = new List<AssetBalanceSummaryDetails>();
            foreach (var group in a.Concat(b).GroupBy(_ => _.Asset))
            {
                AssetBalanceSummaryDetails details = new AssetBalanceSummaryDetails();
                details.Quantity = group.Sum(o => o.Quantity);
                details.Received = group.Sum(o => o.Received);
                details.Asset = group.Key;
                result.Add(details);
            }
            return result.ToArray();
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
                TransactionCount = -c1.TransactionCount,
                Assets = Minus(c1.Assets)
            };
            return result;
        }

        private static AssetBalanceSummaryDetails[] Minus(AssetBalanceSummaryDetails[] a)
        {
            if (a == null)
                return null;
            List<AssetBalanceSummaryDetails> result = new List<AssetBalanceSummaryDetails>();
            foreach (var detail in a)
            {
                var balance = new AssetBalanceSummaryDetails();
                balance.Quantity = -detail.Quantity;
                balance.Received = -detail.Received;
                result.Add(balance);
            }
            return result.ToArray();
        }
#if !CLIENT
        internal static BalanceSummaryDetails CreateFrom(List<OrderedBalanceChange> changes, Network network, bool colored)
        {
            var details = new BalanceSummaryDetails
            {
                Amount = CalculateAmount(changes),
                TransactionCount = changes.Count,
                Received = changes.Select(_ => _.Amount < Money.Zero ? Money.Zero : _.Amount).Sum(),
            };

            if (colored)
            {
                Dictionary<AssetId, AssetBalanceSummaryDetails> coloredDetails = new Dictionary<AssetId, AssetBalanceSummaryDetails>();
                foreach (var change in changes)
                {
                    foreach (var coin in change.ReceivedCoins.OfType<ColoredCoin>())
                    {
                        AssetBalanceSummaryDetails coloredDetail = null;
                        if (!coloredDetails.TryGetValue(coin.AssetId, out coloredDetail))
                        {
                            coloredDetail = new AssetBalanceSummaryDetails();
                            coloredDetail.Asset = coin.AssetId.GetWif(network);
                            coloredDetails.Add(coin.AssetId, coloredDetail);
                        }
                        coloredDetail.Quantity += (long)coin.Asset.Quantity;
                        coloredDetail.Received += (long)coin.Asset.Quantity;
                    }
                    foreach (var coin in change.SpentCoins.OfType<ColoredCoin>())
                    {
                        AssetBalanceSummaryDetails coloredDetail = null;
                        if (!coloredDetails.TryGetValue(coin.AssetId, out coloredDetail))
                        {
                            coloredDetail = new AssetBalanceSummaryDetails();
                            coloredDetail.Asset = coin.AssetId.GetWif(network);
                            coloredDetails.Add(coin.AssetId, coloredDetail);
                        }
                        coloredDetail.Quantity -= (long)coin.Asset.Quantity;
                    }
                }
                details.Assets = coloredDetails.Values.ToArray();
            }
            return details;
        }

        static Money CalculateAmount(IEnumerable<OrderedBalanceChange> changes)
        {
            return changes.SelectMany(c => c.ReceivedCoins.OfType<Coin>()).Select(c => c.Amount).Sum()
                -
                changes.SelectMany(c => c.SpentCoins.OfType<Coin>()).Select(c => c.Amount).Sum();
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
            ReceivedCoins = new List<ICoin>();
            SpentCoins = new List<ICoin>();
        }
#if !CLIENT
        public BalanceOperation(OrderedBalanceChange balanceChange, ChainBase chain)
        {
            ReceivedCoins = balanceChange.ReceivedCoins.ToList();
            SpentCoins = balanceChange.SpentCoins.ToList();
            Amount = balanceChange.Amount;
            TransactionId = balanceChange.TransactionId;
            FirstSeen = balanceChange.SeenUtc;
            if(balanceChange.BlockId != null)
            {
                BlockId = balanceChange.BlockId;
                Height = chain.GetBlock(balanceChange.BlockId).Height;
                Confirmations = (chain.Tip.Height - Height) + 1;
            }
            else
            {
                Height = chain.Tip.Height + 1;
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

        public List<ICoin> ReceivedCoins
        {
            get;
            set;
        }
        public List<ICoin> SpentCoins
        {
            get;
            set;
        }
        public DateTimeOffset FirstSeen
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
