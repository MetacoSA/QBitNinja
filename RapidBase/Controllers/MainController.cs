using Microsoft.WindowsAzure.Storage;
using NBitcoin;
using NBitcoin.Indexer;
using RapidBase.ModelBinders;
using RapidBase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Web.Http;
using System.Web.Http.ModelBinding;

namespace RapidBase.Controllers
{
    public class MainController : ApiController
    {
        public MainController(
            ConcurrentChain chain,
            RapidBaseConfiguration config)
        {
            Configuration = config;
            Chain = chain;
        }
        public ConcurrentChain Chain
        {
            get;
            set;
        }

        public new RapidBaseConfiguration Configuration
        {
            get;
            set;
        }


        [HttpGet]
        [Route("transactions/{txId}")]
        public object Transaction(
            [ModelBinder(typeof(BitcoinSerializableModelBinder))]
            uint256 txId,
            DataFormat format = DataFormat.Json
            )
        {
            if (format == DataFormat.Json)
                return JsonTransaction(txId);

            return RawTransaction(txId);
        }

        [HttpPost]
        [Route("wallets")]
        public WalletModel CreateWallet(WalletModel wallet)
        {
            if (string.IsNullOrEmpty(wallet.Name))
                throw new FormatException("Invalid wallet name");
            var repo = Configuration.CreateWalletRepository();
            repo.Create(wallet);
            return wallet;
        }

        [HttpPost]
        [Route("wallets/{walletName}/addresses")]
        public WalletAddress AddWalletAddresses(
            string walletName,
            [FromBody]WalletAddress address)
        {
            if (address.RedeemScript != null && address.Address == null)
            {
                address.Address = address.RedeemScript.GetScriptAddress(Network);
            }
            if (address.Address == null)
                throw new FormatException("Address is missing");
            var repo = Configuration.CreateWalletRepository();
            repo.AddAddress(walletName, address);
            return address;
        }

        [HttpGet]
        [Route("wallets/{walletName}/addresses")]
        public WalletAddress[] WalletAddresses(string walletName)
        {
            var repo = Configuration.CreateWalletRepository();
            return repo.GetAddresses(walletName);
        }

        [HttpGet]
        [Route("wallets")]
        public WalletModel[] Wallets()
        {
            var repo = Configuration.CreateWalletRepository();
            return repo.Get();
        }

        internal GetTransactionResponse JsonTransaction(uint256 txId)
        {
            var client = Configuration.Indexer.CreateIndexerClient();
            var tx = client.GetTransaction(txId);
            if (tx == null)
                throw new HttpResponseException(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ReasonPhrase = "Transaction not found"
                });
            return new GetTransactionResponse()
            {
                TransactionId = tx.TransactionId,
                Transaction = tx.Transaction,
                IsCoinbase = tx.Transaction.IsCoinBase,
                Fees = tx.Fees,
                Block = FetchBlockInformation(tx.BlockIds),
                SpentCoins = tx.SpentCoins == null ? null : tx.SpentCoins.Select(c => new Coin(c)).ToList()
            };
        }

        private BlockInformation FetchBlockInformation(uint256[] blockIds)
        {
            var confirmed = blockIds.Select(b => Chain.GetBlock(b)).FirstOrDefault();
            if (confirmed == null)
            {
                return null;
            }
            return new BlockInformation
            {
                BlockId = confirmed.HashBlock,
                BlockHeader = confirmed.Header,
                Confirmations = Chain.Tip.Height - confirmed.Height + 1,
                Height = confirmed.Height,
            };
        }

        public HttpResponseMessage RawTransaction(
            uint256 txId
            )
        {
            var client = Configuration.Indexer.CreateIndexerClient();
            var tx = client.GetTransaction(txId);
            if (tx == null)
                throw new HttpResponseException(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ReasonPhrase = "Transaction not found"
                });
            return Response(tx.Transaction);
        }


        public HttpResponseMessage RawBlock(
            BlockFeature blockFeature, bool headerOnly)
        {
            var block = GetBlock(blockFeature, headerOnly);
            if (block == null)
            {
                throw new HttpResponseException(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ReasonPhrase = "Block not found"
                });
            }
            return Response(headerOnly ? (IBitcoinSerializable)block.Header : block);
        }

        [HttpPost]
        [Route("blocks/onnew")]
        public CallbackRegistration OnNewBlock(CallbackRegistration registration)
        {
            var repo = Configuration.CreateCallbackRepository();
            return repo.CreateCallback("onnewblock", registration);
        }

        [HttpDelete]
        [Route("blocks/onnew/{registrationId}")]
        public void OnNewBlock(string registrationId)
        {
            var repo = Configuration.CreateCallbackRepository();
            repo.Delete("onnewblock", registrationId);
        }

        [HttpGet]
        [Route("blocks/onnew")]
        public CallbackRegistration[] OnNewBlock()
        {
            var repo = Configuration.CreateCallbackRepository();
            return repo.GetCallbacks("onnewblock");
        }

        [HttpGet]
        [Route("blocks/{blockFeature}")]
        public object Block(
            [ModelBinder(typeof(BlockFeatureModelBinder))]
            BlockFeature blockFeature, bool headerOnly = false, DataFormat format = DataFormat.Json)
        {
            if (format == DataFormat.Json)
                return JsonBlock(blockFeature, headerOnly);

            return RawBlock(blockFeature, headerOnly);
        }


        [HttpGet]
        [Route("balances/{address}/summary")]
        public BalanceSummary BalanceSummary(
            [ModelBinder(typeof(Base58ModelBinder))]
            BitcoinAddress address,
            [ModelBinder(typeof(BlockFeatureModelBinder))]
            BlockFeature at = null)
        {
            BalanceSummary cachedSummary = null;

            CancellationTokenSource cancel = new CancellationTokenSource();
            cancel.CancelAfter(30000);

            var query = new BalanceQuery();
            var atBlock = Chain.Tip;
            if (at != null)
            {
                var chainedBlock = GetChainedBlock(at);
                if (chainedBlock == null)
                    throw new FormatException("'at' not found in the blockchain");
                query.From = new BalanceLocator(chainedBlock.Height);
                atBlock = chainedBlock;
            }


            int newest = 0;
            query.PageSizes = new[] { 1, 10, 100 };
            var client = Configuration.Indexer.CreateIndexerClient();
            var diff =
                client
                .GetOrderedBalance(address, query)
                .WhereNotExpired(TimeSpan.FromHours(1.0))
                .TakeWhile(_ => !cancel.IsCancellationRequested)
                .TakeWhile(_ =>
                {
                    if (_.CustomData != null &&
                        _.CustomData.Contains("TransactionCount") &&
                        _.BlockId != null &&
                        Chain.GetBlock(_.BlockId) != null) //Quick check, less costly than exception
                    {
                        try
                        {
                            cachedSummary = Serializer.ToObject<BalanceSummary>(_.CustomData);
                            if (atBlock.Height >= cachedSummary.NextMaturityHeight) //Cache invalid. Some coins matured
                            {
                                cachedSummary = null;
                                return true;
                            }
                            return false;
                        }
                        catch
                        {
                        }
                    }
                    if (newest == 0)
                        newest = (_.TransactionId == null ? 0 : _.TransactionId.GetHashCode())
                                 ^
                                 (_.BlockId == null ? 0 : _.BlockId.GetHashCode());
                    return true;
                })
                .AsBalanceSheet(Chain);

            if (cancel.Token.IsCancellationRequested)
            {
                throw new HttpResponseException(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    ReasonPhrase = "The server can't fetch the balance summary because the balance is too big. Please, load it in several step with ?at={blockFeature} parameter. Once fully loaded after all the step, the summary will return in constant time."
                });
            }

            if (cachedSummary != null && newest == cachedSummary.Newest)
            {
                cachedSummary.PrepareForSend(at);
                return cachedSummary;
            }


            cachedSummary = cachedSummary ?? new BalanceSummary()
            {
                Confirmed = new BalanceSummaryDetails(),
                UnConfirmed = new BalanceSummaryDetails(),
                NextMaturityHeight = int.MaxValue
            };

            var unconfs
                = (diff.Unconfirmed.Count == 0 || at != null) ? new List<OrderedBalanceChange>()
                : client
                .GetOrderedBalance(address, new BalanceQuery()
                {
                    PageSizes = new[] { 5, 10 }
                })
                .WhereNotExpired(TimeSpan.FromHours(1.0))
                .TakeWhile(_ => _.Height > Chain.Height - 30)
                .AsBalanceSheet(Chain)
                .Unconfirmed;

            var confs = diff.Confirmed;
            var immature = diff.Confirmed
                                .Concat(unconfs)
                                .Where(c => !IsMature(c, atBlock))
                                .ToList();



            var summary = new BalanceSummary()
            {
                Confirmed = new BalanceSummaryDetails()
                {
                    Amount = confs.Select(_ => _.Amount).Sum(),
                    TransactionCount = confs.Count,
                    Received = confs.Select(_ => _.Amount < Money.Zero ? Money.Zero : _.Amount).Sum(),
                },
                Immature = new BalanceSummaryDetails()
                {
                    Amount = immature.Select(_ => _.Amount).Sum(),
                    TransactionCount = immature.Count,
                    Received = immature.Select(_ => _.Amount < Money.Zero ? Money.Zero : _.Amount).Sum(),
                },
                UnConfirmed = new BalanceSummaryDetails()
                {
                    Amount = unconfs.Select(_ => _.Amount).Sum(),
                    TransactionCount = unconfs.Count,
                    Received = unconfs.Select(_ => _.Amount < Money.Zero ? Money.Zero : _.Amount).Sum(),
                },
            };
            summary.Confirmed += cachedSummary.Confirmed;
            summary.Immature += cachedSummary.Immature;
            summary.NextMaturityHeight = int.MaxValue;
            var olderImmature = immature.Where(i => i.BlockId != null).MinElement(i => i.Height);
            if (olderImmature != null)
            {
                summary.NextMaturityHeight = Math.Min(olderImmature.Height + Configuration.CoinbaseMaturity - 1, cachedSummary.NextMaturityHeight);
            }


            var cacheBearer = diff.Confirmed.Count != 0 ? diff.Confirmed[0] :
                diff.Unconfirmed.Count != 0 ? diff.Unconfirmed[0] : null;


            if (cacheBearer != null)
            {
                if (cacheBearer.Height == atBlock.Height)
                {
                    summary.Newest = newest;
                    cacheBearer.CustomData = Serializer.ToString(summary);
                    Configuration.Indexer.CreateIndexer().Index(new[] { cacheBearer });
                }

            }

            summary.PrepareForSend(at);
            return summary;
        }

        private bool IsMature(OrderedBalanceChange c, ChainedBlock tip)
        {
            return !c.IsCoinbase ||
                    (
                        c.BlockId != null &&
                        (tip.Height - c.Height + 1) >= Configuration.CoinbaseMaturity
                    );
        }

        [HttpGet]
        [Route("balances/{address}")]
        public BalanceModel Balance(
            [ModelBinder(typeof(Base58ModelBinder))]
            BitcoinAddress address,
            [ModelBinder(typeof(BalanceLocatorModelBinder))]
            BalanceLocator continuation = null,
            [ModelBinder(typeof(BalanceLocatorModelBinder))]
            BalanceLocator until = null,
            [ModelBinder(typeof(BalanceLocatorModelBinder))]
            BalanceLocator from = null,
            bool unspentOnly = false)
        {
            CancellationTokenSource cancel = new CancellationTokenSource();
            cancel.CancelAfter(30000);

            BalanceQuery query = null;
            if (continuation != null)
            {
                query = new BalanceQuery();
                query.From = continuation;
                query.FromIncluded = false;
            }
            if (from != null)
            {
                if (query == null)
                    query = new BalanceQuery();
                query.From = from;
                query.FromIncluded = true;
            }
            if (until != null)
            {
                if (query == null)
                    query = new BalanceQuery();
                query.To = until;
            }

            var client = Configuration.Indexer.CreateIndexerClient();
            var balance =
                client
                .GetOrderedBalance(address, query)
                .TakeWhile(_ => !cancel.IsCancellationRequested)
                .AsBalanceSheet(Chain);

            var balanceChanges = balance.All.WhereNotExpired().ToList();
            if (unspentOnly)
            {
                var changeByTxId = balanceChanges.ToDictionary(_ => _.TransactionId);
                var spentOutpoints = changeByTxId.Values.SelectMany(b => b.SpentCoins.Select(c => c.Outpoint)).ToDictionary(_ => _);
                foreach (var change in changeByTxId.Values.ToArray())
                {
                    change.SpentCoins.Clear();
                    change.ReceivedCoins.RemoveAll(c => spentOutpoints.ContainsKey(c.Outpoint));
                }
            }

            var result = new BalanceModel(balanceChanges, Chain);
            if (cancel.IsCancellationRequested)
            {
                if (balanceChanges.Count > 0)
                {
                    var lastop = balanceChanges[balanceChanges.Count - 1];
                    result.Continuation = lastop.CreateBalanceLocator();
                }
            }
            return result;
        }

        [HttpGet]
        [Route("whatisit/{data}")]
        public object WhatIsIt(string data)
        {
            WhatIsIt finder = new WhatIsIt(this);
            return finder.Find(data) ?? "Good question Holmes !";
        }

        public Network Network
        {
            get
            {
                return Configuration.Indexer.Network;
            }
        }

        internal GetBlockResponse JsonBlock(BlockFeature blockFeature, bool headerOnly)
        {
            var block = GetBlock(blockFeature, headerOnly);
            if (block == null)
            {
                throw new HttpResponseException(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ReasonPhrase = "Block not found"
                });
            }
            return new GetBlockResponse()
            {
                AdditionalInformation = FetchBlockInformation(new[] { block.Header.GetHash() }) ?? new BlockInformation(block.Header),
                Block = headerOnly ? null : block
            };
        }

        private ChainedBlock GetChainedBlock(BlockFeature blockFeature)
        {
            ChainedBlock chainedBlock;
            if (blockFeature.Special != null && blockFeature.Special.Value == SpecialFeature.Last)
            {
                chainedBlock = Chain.Tip;
            }
            else if (blockFeature.Height != -1)
            {
                var h = Chain.GetBlock(blockFeature.Height);
                if (h == null)
                    return null;
                chainedBlock = h;
            }
            else
            {
                chainedBlock = Chain.GetBlock(blockFeature.BlockId);
            }
            return chainedBlock;
        }

        private Block GetBlock(BlockFeature blockFeature, bool headerOnly)
        {
            var chainedBlock = GetChainedBlock(blockFeature);
            var hash = chainedBlock == null ? blockFeature.BlockId : chainedBlock.HashBlock;
            if (hash == null)
                return null;
            var client = Configuration.Indexer.CreateIndexerClient();
            return headerOnly ? GetHeader(hash, client) : client.GetBlock(hash);
        }

        private Block GetHeader(uint256 hash, IndexerClient client)
        {
            var header = Chain.GetBlock(hash);
            if (header == null)
            {
                var b = client.GetBlock(hash);
                if (b == null)
                    return null;
                return new Block(b.Header);
            }
            return new Block(header.Header);
        }

        private static HttpResponseMessage Response(IBitcoinSerializable obj)
        {
            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(obj.ToBytes())
            };
            result.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/octet-stream");
            return result;
        }
    }
}
