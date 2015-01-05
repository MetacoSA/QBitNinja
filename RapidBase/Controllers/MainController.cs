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
            BitcoinAddress address)
        {
            BalanceSummary cachedSummary = null;

            CancellationTokenSource cancel = new CancellationTokenSource();
            cancel.CancelAfter(30000);


            int newest = 0;

            var client = Configuration.Indexer.CreateIndexerClient();
            var diff =
                client
                .GetOrderedBalance(address)
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

            if (cachedSummary != null && newest == cachedSummary.Newest)
            {
                cachedSummary.Newest = 0;
                return cachedSummary;
            }

            cancel.Token.ThrowIfCancellationRequested();
            cachedSummary = cachedSummary ?? new BalanceSummary();

            var unconfs
                = diff.Unconfirmed.Count == 0 ? new List<OrderedBalanceChange>()
                : client
                .GetOrderedBalance(address)
                .WhereNotExpired(TimeSpan.FromHours(1.0))
                .TakeWhile(_ => _.Height > Chain.Height - 30)
                .AsBalanceSheet(Chain)
                .Unconfirmed;

            var summary = new BalanceSummary()
            {
                Confirmed = new BalanceSummaryDetails()
                {
                    Amount = diff.Confirmed.Select(_=>_.Amount).Sum() + cachedSummary.Confirmed.Amount,
                    TransactionCount = diff.Confirmed.Count + cachedSummary.Confirmed.TransactionCount,
                    Received = diff.Confirmed.Select(_=>_.Amount < Money.Zero ? Money.Zero : _.Amount).Sum() + cachedSummary.Confirmed.Received,
                },
                Pending = new BalanceSummaryDetails()
                {
                    Amount = unconfs.Select(_ => _.Amount).Sum(),
                    TransactionCount = unconfs.Count,
                    Received = unconfs.Select(_ => _.Amount < Money.Zero ? Money.Zero : _.Amount).Sum(),
                }
            };

            var cacheBearer = diff.Confirmed.Count != 0 ? diff.Confirmed[0] :
                diff.Unconfirmed.Count != 0 ? diff.Unconfirmed[0] : null;


            if (cacheBearer != null)
            {
                summary.Newest = newest;
                cacheBearer.CustomData = Serializer.ToString(summary);
                summary.Newest = 0;
                Configuration.Indexer.CreateIndexer().Index(new[] { cacheBearer });
            }

            return summary;
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

        private Block GetBlock(BlockFeature blockFeature, bool headerOnly)
        {
            var client = Configuration.Indexer.CreateIndexerClient();
            uint256 hash;
            if (blockFeature.Special != null && blockFeature.Special.Value == SpecialFeature.Last)
            {
                hash = Chain.Tip.HashBlock;
            }
            else if (blockFeature.Height != -1)
            {
                var h = Chain.GetBlock(blockFeature.Height);
                if (h == null)
                    return null;
                hash = h.HashBlock;
            }
            else
            {
                hash = blockFeature.BlockId;
            }
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
