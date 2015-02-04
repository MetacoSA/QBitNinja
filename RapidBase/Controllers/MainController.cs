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
            if (!repo.Create(wallet))
                throw Error(409, "wallet already exist");
            return wallet;
        }

        private Exception Error(int httpCode, string reason)
        {
            return new RapidBaseException(httpCode, reason);
        }

        [HttpGet]
        [Route("wallets/{walletName}/balance")]
        public BalanceModel WalletBalance(
            string walletName,
            [ModelBinder(typeof(BalanceLocatorModelBinder))]
            BalanceLocator continuation = null,
            [ModelBinder(typeof(BlockFeatureModelBinder))]
            BlockFeature until = null,
            [ModelBinder(typeof(BlockFeatureModelBinder))]
            BlockFeature from = null,
            bool includeImmature = false,
            bool unspentOnly = false,
            bool colored = false)
        {
            var balanceId = new BalanceId(walletName);
            return Balance(balanceId, continuation, until, from, includeImmature, unspentOnly, colored);
        }

        [HttpPost]
        [Route("wallets/{walletname}/addresses")]
        public WalletAddress AddWalletAddresses(
            string walletName,
            [FromBody]InsertWalletAddress insertAddress)
        {
            var address = insertAddress.Address;
            if (address.RedeemScript != null && address.Address == null)
            {
                address.Address = address.RedeemScript.GetScriptAddress(Network);
            }
            if (address.Address == null)
                throw Error(400, "Address is missing");

            if (!address.IsCoherent())
                throw Error(400, "The provided redeem script does not correspond to the given address");

            var repo = Configuration.CreateWalletRepository();
            var rule = repo.AddAddress(walletName, address);
            if (rule == null)
                throw Error(409, "This address already exist in the wallet");

            if (insertAddress.MergePast)
            {
                var index = Configuration.Indexer.CreateIndexerClient();
                CancellationTokenSource cancel = new CancellationTokenSource();
                cancel.CancelAfter(10000);
                index.MergeIntoWallet(walletName, address.Address, rule, cancel.Token);
            }
            return address;
        }

        [HttpGet]
        [Route("wallets/{walletName}/addresses")]
        public WalletAddress[] WalletAddresses(string walletName)
        {
            var repo = Configuration.CreateWalletRepository();
            return repo.GetAddresses(walletName);
        }

        [HttpDelete]
        [Route("wallets/{walletName}/keysets/{keyset}")]
        public bool DeleteKeyset(string walletName, string keyset)
        {
            var repo = Configuration.CreateWalletRepository();
            if (!repo.DeleteKeySet(walletName, keyset))
            {
                throw Error(404, "keyset not found");
            }
            return true;
        }

        [HttpPost]
        [Route("wallets/{walletName}/keysets")]
        public HDKeySet CreateKeyset(string walletName, [FromBody]HDKeySet keyset)
        {
            if (keyset.ExtPubKeys == null || keyset.ExtPubKeys.Length == 0)
                throw Error(400, "ExtPubKeys not specified");
            if (keyset.ExtPubKeys.Length < keyset.SignatureCount)
                throw Error(400, "SignatureCount should not be higher than the number of HD Keys");
            var repo = Configuration.CreateWalletRepository();
            if (!repo.AddKeySet(walletName, keyset))
                throw Error(409, "Keyset already exists");
            return keyset;
        }

        [HttpGet]
        [Route("wallets/{walletName}/keysets")]
        public KeySetData[] CreateKeyset(string walletName)
        {
            var repo = Configuration.CreateWalletRepository();
            return repo.GetKeysets(walletName);
        }

        [HttpPost]
        [Route("wallets/{walletName}/keysets/{keysetName}/keys")]
        public HDKeyData Generate(string walletName, string keysetName)
        {
            var repo = Configuration.CreateWalletRepository();
            return repo.NewKey(walletName, keysetName);
        }
        [HttpGet]
        [Route("wallets/{walletName}/keysets/{keysetName}/keys")]
        public HDKeyData[] GetKeys(string walletName, string keysetName)
        {
            var repo = Configuration.CreateWalletRepository();
            return repo.GetKeys(walletName, keysetName);
        }

        [HttpGet]
        [Route("wallets/{walletName}/summary")]
        public BalanceSummary AddressBalanceSummary(
            string walletName,
            [ModelBinder(typeof(BlockFeatureModelBinder))]
            BlockFeature at = null,
            bool debug = false,
            bool colored = false)
        {
            BalanceId id = new BalanceId(walletName);
            return BalanceSummary(id, at, debug, colored);
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
        [Route("blocks/{blockFeature}/header")]
        public WhatIsBlockHeader BlockHeader(
            [ModelBinder(typeof(BlockFeatureModelBinder))]
            BlockFeature blockFeature)
        {
            var block = GetBlock(blockFeature, true);
            return new WhatIsBlockHeader(block.Header);
        }

        [HttpGet]
        [Route("balances/{address}/summary")]
        public BalanceSummary AddressBalanceSummary(
            [ModelBinder(typeof(Base58ModelBinder))]
            IDestination address,
            [ModelBinder(typeof(BlockFeatureModelBinder))]
            BlockFeature at = null,
            bool debug = false,
            bool colored = false)
        {
            BalanceId id = new BalanceId(address);
            colored = address is BitcoinColoredAddress || colored;
            return BalanceSummary(id, at, debug, colored);
        }

        public BalanceSummary BalanceSummary(
            BalanceId balanceId,
            BlockFeature at,
            bool debug,
            bool colored
            )
        {
            CancellationTokenSource cancel = new CancellationTokenSource();
            cancel.CancelAfter(30000);

            var atBlock = AtBlock(at);

            var query = new BalanceQuery();
            if (at != null)
                query.From = ToBalanceLocator(atBlock);

            query.PageSizes = new[] { 1, 10, 100 };

            var cacheTable = Configuration.GetChainCacheTable<BalanceSummary>((colored ? "colsum-" : "balsum-") + balanceId);
            var cachedSummary = cacheTable.Query(Chain, query)
                                          .Where(c =>
                                              (((ConfirmedBalanceLocator)c.Locator).BlockHash == atBlock.HashBlock && at != null) ||
                                              c.Immature.TransactionCount == 0 ||
                                              ((c.Immature.TransactionCount != 0) && !IsMature(c.OlderImmature, atBlock)))
                                          .FirstOrDefault();

            var cachedLocator = cachedSummary == null ? null : (ConfirmedBalanceLocator)cachedSummary.Locator;
            if (cachedSummary != null && at != null && cachedLocator.Height == atBlock.Height)
            {
                cachedSummary.CacheHit = CacheHit.FullCache;
                cachedSummary.PrepareForSend(at, debug);
                return cachedSummary;
            }

            cachedSummary = cachedSummary ?? new BalanceSummary()
            {
                Confirmed = new BalanceSummaryDetails(),
                UnConfirmed = new BalanceSummaryDetails(),
                OlderImmature = int.MaxValue
            };

            int stopAtHeight = cachedSummary.Locator == null ? -1 : cachedLocator.Height;
            if (at == null) //Need more block to find the unconfs
                stopAtHeight = stopAtHeight - 12;

            var client = Configuration.Indexer.CreateIndexerClient();
            client.ColoredBalance = colored;
            var diff =
                client
                .GetOrderedBalance(balanceId, query)
                .WhereNotExpired(TimeSpan.FromHours(1.0))
                .TakeWhile(_ => !cancel.IsCancellationRequested)
                .TakeWhile(_ => _.BlockId == null || _.Height > stopAtHeight)
                .AsBalanceSheet(Chain);

            if (cancel.Token.IsCancellationRequested)
            {
                throw new HttpResponseException(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    ReasonPhrase = "The server can't fetch the balance summary because the balance is too big. Please, load it in several step with ?at={blockFeature} parameter. Once fully loaded after all the step, the summary will return in constant time."
                });
            }

            var unconfs = diff.Unconfirmed;
            var confs = cachedLocator == null ?
                                            diff.Confirmed :
                                            diff.Confirmed.Where(c => c.Height > cachedLocator.Height).ToList();

            var immature = confs.Where(c => !IsMature(c, atBlock)).ToList();


            var summary = new BalanceSummary()
            {
                Confirmed = BalanceSummaryDetails.CreateFrom(confs, Network, colored),
                Immature = BalanceSummaryDetails.CreateFrom(immature, Network, colored),
                UnConfirmed = BalanceSummaryDetails.CreateFrom(unconfs, Network, colored),
            };
            summary.Confirmed += cachedSummary.Confirmed;
            summary.Immature += cachedSummary.Immature;
            summary.Locator = new ConfirmedBalanceLocator(atBlock.Height, atBlock.HashBlock);
            summary.CacheHit = cachedSummary.Locator == null ? CacheHit.NoCache : CacheHit.PartialCache;

            var newCachedLocator = (ConfirmedBalanceLocator)summary.Locator;

            if (
                cachedSummary.Locator == null ||
                newCachedLocator.BlockHash != cachedLocator.BlockHash)
            {
                var olderImmature = immature.Select(_ => _.Height).Concat(new[] { int.MaxValue }).Min();
                var newCachedSummary = new Models.BalanceSummary()
                {
                    Confirmed = summary.Confirmed,
                    Immature = summary.Immature,
                    Locator = summary.Locator,
                    OlderImmature = Math.Min(cachedSummary.OlderImmature, olderImmature)
                };
                cacheTable.Create(newCachedLocator, newCachedSummary);
            }


            summary.PrepareForSend(at, debug);
            return summary;
        }

        private ConfirmedBalanceLocator ToBalanceLocator(BlockFeature feature)
        {
            return ToBalanceLocator(AtBlock(feature));
        }

        private ConfirmedBalanceLocator ToBalanceLocator(ChainedBlock atBlock)
        {
            return new ConfirmedBalanceLocator(atBlock.Height, atBlock.HashBlock);
        }

        private ChainedBlock AtBlock(BlockFeature at)
        {
            var atBlock = Chain.Tip;
            if (at != null)
            {
                var chainedBlock = at.GetChainedBlock(Chain);
                if (chainedBlock == null)
                    throw new FormatException("'at' not found in the blockchain");
                atBlock = chainedBlock;
            }
            return atBlock;
        }

        private bool IsMature(int height, ChainedBlock tip)
        {
            return (tip.Height - height + 1) >= Configuration.CoinbaseMaturity;
        }

        private bool IsMature(OrderedBalanceChange c, ChainedBlock tip)
        {
            return !c.IsCoinbase || (c.BlockId != null && IsMature(c.Height, tip));
        }

        [HttpGet]
        [Route("balances/{address}")]
        public BalanceModel AddressBalance(
            [ModelBinder(typeof(Base58ModelBinder))]
            IDestination address,
            [ModelBinder(typeof(BalanceLocatorModelBinder))]
            BalanceLocator continuation = null,
            [ModelBinder(typeof(BlockFeatureModelBinder))]
            BlockFeature until = null,
            [ModelBinder(typeof(BlockFeatureModelBinder))]
            BlockFeature from = null,
            bool includeImmature = false,
            bool unspentOnly = false,
            bool colored = false)
        {
            var balanceId = new BalanceId(address);
            colored = address is BitcoinColoredAddress || colored;
            return Balance(balanceId, continuation, until, from, includeImmature, unspentOnly, colored);
        }


        BalanceModel Balance(BalanceId balanceId,
            BalanceLocator continuation,
            BlockFeature until,
            BlockFeature from,
            bool includeImmature,
            bool unspentOnly,
            bool colored)
        {
            CancellationTokenSource cancel = new CancellationTokenSource();
            cancel.CancelAfter(30000);

            BalanceQuery query = new BalanceQuery();
            if (continuation != null)
            {
                query = new BalanceQuery();
                query.From = continuation;
                query.FromIncluded = false;
            }

            if (from != null)
            {
                query.From = ToBalanceLocator(from);
                query.FromIncluded = true;
            }
            if (until != null)
            {
                query.To = ToBalanceLocator(until);
                query.FromIncluded = true;


            }

            if (query != null)
            {
                if (query.To.YoungerThan(query.From))
                    throw InvalidParameters("Invalid agurment : from < until");
            }

            var client = Configuration.Indexer.CreateIndexerClient();
            client.ColoredBalance = colored;
            var balance =
                client
                .GetOrderedBalance(balanceId, query)
                .TakeWhile(_ => !cancel.IsCancellationRequested)
                .WhereNotExpired()
                .Where(o => includeImmature || IsMature(o, Chain.Tip))
                .AsBalanceSheet(Chain);



            var balanceChanges = balance.All;
            if (until != null && balance.Confirmed.Count != 0) //Strip unconfirmed that can appear after the last until
            {
                for (int i = balanceChanges.Count - 1 ; i >= 0 ; i--)
                {
                    var last = balanceChanges[i];
                    if (last.BlockId == null)
                        balanceChanges.RemoveAt(i);
                    else
                        break;
                }
            }
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

        private Exception InvalidParameters(string message)
        {
            return new HttpResponseException(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    ReasonPhrase = message
                });
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
            var chainedBlock = blockFeature.GetChainedBlock(Chain);
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
