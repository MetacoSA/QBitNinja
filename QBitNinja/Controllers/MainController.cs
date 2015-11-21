using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QBitNinja.ModelBinders;
using QBitNinja.Models;
using QBitNinja.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.ModelBinding;

namespace QBitNinja.Controllers
{
    public class MainController : ApiController
    {
        public MainController(
            ConcurrentChain chain,
            QBitNinjaConfiguration config)
        {
            Configuration = config;
            Chain = chain;
        }
        public ConcurrentChain Chain
        {
            get;
            set;
        }

        public new QBitNinjaConfiguration Configuration
        {
            get;
            set;
        }

        [HttpPost]
        [Route("transactions")]
        public async Task<BroadcastResponse> Broadcast()
        {
            Transaction tx = null;
            switch(this.Request.Content.Headers.ContentType.MediaType)
            {
                case "application/json":
                    tx = NBitcoin.Transaction.Parse(JsonConvert.DeserializeObject<string>(await Request.Content.ReadAsStringAsync()));
                    break;
                case "application/octet-stream":
                    {
                        tx = new Transaction(await Request.Content.ReadAsByteArrayAsync());
                        break;
                    }
                default:
                    throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }
            await Configuration
                .Topics
                .BroadcastedTransactions
                .AddAsync(new BroadcastedTransaction(tx));

            var hash = tx.GetHash();
            for(int i = 0; i < 10; i++)
            {
                var indexed = await Configuration.Indexer.CreateIndexerClient().GetTransactionAsync(hash);
                if(indexed != null)
                    return new BroadcastResponse()
                    {
                        Success = true
                    };
                var reject = await Configuration.GetRejectTable().ReadOneAsync(hash.ToString());
                if(reject != null)
                    return new BroadcastResponse()
                    {
                        Success = false,
                        Error = new BroadcastError()
                        {
                            ErrorCode = reject.Code,
                            Reason = reject.Reason
                        }
                    };
                await Task.Delay(100 * i);
            }
            return new BroadcastResponse()
            {
                Success = true,
                Error = new BroadcastError()
                {
                    ErrorCode = NBitcoin.Protocol.RejectCode.INVALID,
                    Reason = "Unknown"
                }
            };
        }

        [HttpGet]
        [Route("transactions/{txId}")]
        public async Task<object> Transaction(
            [ModelBinder(typeof(BitcoinSerializableModelBinder))]
            uint256 txId,
            DataFormat format = DataFormat.Json,
            bool colored = false
            )
        {
            if(format == DataFormat.Json)
                return await JsonTransaction(txId, colored);

            return await RawTransaction(txId);
        }

        [HttpPost]
        [Route("wallets")]
        public WalletModel CreateWallet(WalletModel wallet)
        {
            if(string.IsNullOrEmpty(wallet.Name))
                throw new FormatException("Invalid wallet name");
            AssertValidUrlPart(wallet.Name, "wallet name");
            var repo = Configuration.CreateWalletRepository();
            if(!repo.Create(wallet))
                throw Error(409, "wallet already exist");
            return wallet;
        }

        [HttpPost]
        [Route("subscriptions")]
        public async Task<Subscription> AddSubscription(Subscription subscription)
        {
            if(subscription.Id == null)
                subscription.Id = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));

            if(!await (Configuration
            .GetSubscriptionsTable()
            .CreateAsync(subscription.Id, subscription, false)))
            {
                throw Error(409, "notification already exist");
            }

            await Configuration
                .Topics
                .SubscriptionChanges
                .AddAsync(new SubscriptionChange(subscription, true));
            return subscription;
        }

        private void AssertValidUrlPart(string str, string fieldName)
        {
            if(str.Contains('/') || str.Contains('?'))
                throw Error(400, "A field contains illegal characters (" + fieldName + ")");
        }

        private Exception Error(int httpCode, string reason)
        {
            return new QBitNinjaException(httpCode, reason);
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
            return AddWalletAddressesCore(walletName, insertAddress, null);
        }

        private WalletAddress AddWalletAddressesCore(string walletName, InsertWalletAddress insertAddress, Dictionary<string, JObject> additionalProperties)
        {
            if(additionalProperties == null)
                additionalProperties = new Dictionary<string, JObject>();
            if(insertAddress.RedeemScript != null && insertAddress.Address == null)
            {
                insertAddress.Address = insertAddress.RedeemScript.GetScriptAddress(Network);
            }
            if(insertAddress.Address == null || !((insertAddress.Address) is IDestination))
                throw Error(400, "Address is missing");

            if(!insertAddress.IsCoherent())
                throw Error(400, "The provided redeem script does not correspond to the given address");

            var address = new WalletAddress();
            address.Address = insertAddress.Address;
            address.RedeemScript = insertAddress.RedeemScript;
            address.AdditionalInformation = ToAdditionalInformation(insertAddress, additionalProperties);
            address.WalletName = walletName;

            var repo = Configuration.CreateWalletRepository();
            var walletRule = repo.AddAddress(address);
            if(walletRule == null)
                throw Error(409, "This address already exist in the wallet");


            var unused = Configuration.Topics.AddedAddresses.AddAsync(address);
            var rule = walletRule.Rule;
            bool merge = false;
            if(insertAddress.MergePast)
            {
                var index = Configuration.Indexer.CreateIndexerClient();
                CancellationTokenSource cancel = new CancellationTokenSource();
                cancel.CancelAfter(10000);
                merge = index.MergeIntoWallet(walletName, address, rule, cancel.Token);
            }
            if(merge)
            {
                GetBalanceSummaryCacheTable(new BalanceId(walletName), true).Delete();
                GetBalanceSummaryCacheTable(new BalanceId(walletName), false).Delete();
            }
            return address;
        }


        private static JObject ToAdditionalInformation(InsertWalletAddress address, Dictionary<string, JObject> properties)
        {
            JObject obj = new JObject();
            obj.Add("userData", address.UserData ?? new JValue(""));
            foreach(var kv in properties)
            {
                obj.Add(kv.Key, kv.Value);
            }
            return obj;
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
            if(!repo.DeleteKeySet(walletName, keyset))
            {
                throw Error(404, "keyset not found");
            }
            return true;
        }

        [HttpPost]
        [Route("wallets/{walletName}/keysets")]
        public HDKeySet CreateKeyset(string walletName, [FromBody]HDKeySet keyset)
        {
            AssertValidUrlPart(keyset.Name, "Keyset name");
            if(keyset.ExtPubKeys == null || keyset.ExtPubKeys.Length == 0)
                throw Error(400, "ExtPubKeys not specified");
            if(keyset.ExtPubKeys.Length < keyset.SignatureCount)
                throw Error(400, "SignatureCount should not be higher than the number of HD Keys");
            if(keyset.Path != null && keyset.Path.ToString().Contains("'"))
                throw Error(400, "The keypath should not contains hardened children");
            var repo = Configuration.CreateWalletRepository();
            if(!repo.AddKeySet(walletName, keyset))
                throw Error(409, "Keyset already exists");
            return keyset;
        }

        [HttpGet]
        [Route("wallets/{walletName}/keysets")]
        public KeySetData[] GetKeysets(string walletName)
        {
            var repo = Configuration.CreateWalletRepository();
            var sets = repo.GetKeysets(walletName);
            if(sets.Length == 0)
                AssetWalletAndKeysetExists(walletName, null);
            return sets;
        }

        [HttpPost]
        [Route("wallets/{walletName}/keysets/{keysetName}/keys")]
        public HDKeyData Generate(string walletName, string keysetName)
        {
            var repo = Configuration.CreateWalletRepository();
            var keyset = repo.GetKeySetData(walletName, keysetName);
            var key = repo.NewKey(walletName, keysetName);
            if(key != null)
            {
                keyset.State = new HDKeyState()
                {
                    CurrentPath = key.Path
                };
                AddWalletAddressesCore(walletName, new InsertWalletAddress()
                {
                    Address = key.Address,
                    RedeemScript = key.RedeemScript,
                    MergePast = true
                }, new Dictionary<string, JObject>()
                {
                     { "keysetData", JObject.Parse(Serializer.ToString<KeySetData>(keyset)) }
                });
                return key;
            }
            AssetWalletAndKeysetExists(walletName, keysetName);
            throw Error(500, "Unknown error about the keyset");
        }

        private void AssetWalletAndKeysetExists(string walletName, string keysetName)
        {
            var repo = Configuration.CreateWalletRepository();
            var wallet = repo.GetWallet(walletName);
            if(wallet == null)
                throw Error(404, "wallet does not exists");
            if(keysetName != null)
            {
                var keyset = repo.GetKeySetData(walletName, keysetName);
                if(keyset == null)
                {
                    throw Error(404, "keyset does not exists");
                }
            }
        }
        [HttpGet]
        [Route("wallets/{walletName}/keysets/{keysetName}/keys")]
        public HDKeyData[] GetKeys(string walletName, string keysetName)
        {
            var repo = Configuration.CreateWalletRepository();
            var keys = repo.GetKeys(walletName, keysetName);
            if(keys.Length == 0)
            {
                AssetWalletAndKeysetExists(walletName, keysetName);
            }
            return keys;
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


        [HttpGet]
        [Route("wallets/{walletName}")]
        public WalletModel GetWallet(string walletName)
        {
            var repo = Configuration.CreateWalletRepository();
            var result = repo.GetWallet(walletName);
            if(result == null)
                throw Error(404, "Wallet not found");
            return result;
        }

        internal async Task<GetTransactionResponse> JsonTransaction(uint256 txId, bool colored)
        {
            var client = Configuration.Indexer.CreateIndexerClient();
            var tx = await client.GetTransactionAsync(true, colored, txId);
            if(tx == null || (tx.ColoredTransaction == null && colored) || tx.SpentCoins == null)
                throw new HttpResponseException(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ReasonPhrase = "Transaction not found"
                });

            var response = new GetTransactionResponse()
            {
                TransactionId = tx.TransactionId,
                Transaction = tx.Transaction,
                IsCoinbase = tx.Transaction.IsCoinBase,
                Fees = tx.Fees,
                Block = FetchBlockInformation(tx.BlockIds),
                FirstSeen = tx.FirstSeen
            };
            for(int i = 0; i < tx.Transaction.Outputs.Count; i++)
            {
                var txout = tx.Transaction.Outputs[i];
                ICoin coin = new Coin(tx.Transaction, txout);
                if(colored)
                {
                    var entry = tx.ColoredTransaction.GetColoredEntry((uint)i);
                    if(entry != null)
                        coin = new ColoredCoin(entry.Asset, (Coin)coin);
                }
                response.ReceivedCoins.Add(coin);
            }
            if(!response.IsCoinbase)
                for(int i = 0; i < tx.Transaction.Inputs.Count; i++)
                {
                    ICoin coin = new Coin(tx.SpentCoins[i].OutPoint, tx.SpentCoins[i].TxOut);
                    if(colored)
                    {
                        var entry = tx.ColoredTransaction.Inputs.FirstOrDefault(ii => ii.Index == i);
                        if(entry != null)
                            coin = new ColoredCoin(entry.Asset, (Coin)coin);
                    }
                    response.SpentCoins.Add(coin);
                }
            return response;
        }

        private BlockInformation FetchBlockInformation(uint256[] blockIds)
        {
            var confirmed = blockIds.Select(b => Chain.GetBlock(b)).FirstOrDefault();
            if(confirmed == null)
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

        public async Task<HttpResponseMessage> RawTransaction(
            uint256 txId
            )
        {
            var client = Configuration.Indexer.CreateIndexerClient();
            var tx = await client.GetTransactionAsync(txId);
            if(tx == null)
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
            if(block == null)
            {
                throw new HttpResponseException(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ReasonPhrase = "Block not found"
                });
            }
            return Response(headerOnly ? (IBitcoinSerializable)block.Header : block);
        }

        [HttpGet]
        [Route("blocks/{blockFeature}")]
        public object Block(
            [ModelBinder(typeof(BlockFeatureModelBinder))]
            BlockFeature blockFeature, bool headerOnly = false, DataFormat format = DataFormat.Json)
        {
            if(format == DataFormat.Json)
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
            var checkpoint = Configuration.Indexer.CreateIndexer()
                .GetCheckpoint(balanceId.Type == BalanceType.Address ? IndexerCheckpoints.Balances : IndexerCheckpoints.Wallets);

            var atBlock = AtBlock(at);

            var query = new BalanceQuery();
            //query.From = null;
            if(at != null)
                query.From = ToBalanceLocator(atBlock);

            //if (query.From == null)
            //    query.From = new UnconfirmedBalanceLocator(DateTimeOffset.UtcNow - TimeSpan.FromHours(24.0));

            query.PageSizes = new[] { 1, 10, 100 };

            var cacheTable = GetBalanceSummaryCacheTable(balanceId, colored);
            var cachedSummary = cacheTable.Query(Chain, query).FirstOrDefault(c => (((ConfirmedBalanceLocator)c.Locator).BlockHash == atBlock.HashBlock && at != null) ||
                                                                                   c.Immature.TransactionCount == 0 ||
                                                                                   ((c.Immature.TransactionCount != 0) && !IsMature(c.OlderImmature, atBlock)));

            var cachedLocator = cachedSummary == null ? null : (ConfirmedBalanceLocator)cachedSummary.Locator;
            if(cachedSummary != null && at != null && cachedLocator.Height == atBlock.Height)
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
            if(at == null) //Need more block to find the unconfs
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

            if(cancel.Token.IsCancellationRequested)
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

            if(
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
                var checkpointBlock = Chain.GetBlock(checkpoint.BlockLocator.Blocks[0]);
                if(checkpointBlock != null && checkpointBlock.Height >= atBlock.Height)
                    cacheTable.Create(newCachedLocator, newCachedSummary);
            }

            summary.PrepareForSend(at, debug);
            return summary;
        }

        private ChainTable<Models.BalanceSummary> GetBalanceSummaryCacheTable(BalanceId balanceId, bool colored)
        {
            Scope scope = new Scope(new[] { balanceId.ToString() });
            scope = scope.GetChild(colored ? "colsum" : "balsum");
            var cacheTable = Configuration.GetChainCacheTable<BalanceSummary>(scope);
            return cacheTable;
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
            if(at != null)
            {
                var chainedBlock = at.GetChainedBlock(Chain);
                if(chainedBlock == null)
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
            query.From = null;

            if(from != null)
            {
                query.From = ToBalanceLocator(from);
                query.FromIncluded = true;
            }

            if(continuation != null)
            {
                query = new BalanceQuery
                {
                    From = continuation,
                    FromIncluded = false
                };
            }

            if(query.From == null)
            {
                query.From = new UnconfirmedBalanceLocator(DateTimeOffset.UtcNow - TimeSpan.FromHours(24.0));
            }

            if(until != null)
            {
                query.To = ToBalanceLocator(until);
                query.FromIncluded = true;
            }

            if(query.To.IsGreaterThan(query.From))
                throw InvalidParameters("Invalid agurment : from < until");

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
            if(until != null && balance.Confirmed.Count != 0) //Strip unconfirmed that can appear after the last until
            {
                for(int i = balanceChanges.Count - 1; i >= 0; i--)
                {
                    var last = balanceChanges[i];
                    if(last.BlockId == null)
                        balanceChanges.RemoveAt(i);
                    else
                        break;
                }
            }
            if(unspentOnly)
            {
                var changeByTxId = balanceChanges.ToDictionary(_ => _.TransactionId);
                var spentOutpoints = changeByTxId.Values.SelectMany(b => b.SpentCoins.Select(c => c.Outpoint)).ToDictionary(_ => _);
                foreach(var change in changeByTxId.Values.ToArray())
                {
                    change.SpentCoins.Clear();
                    change.ReceivedCoins.RemoveAll(c => spentOutpoints.ContainsKey(c.Outpoint));
                }
            }

            var result = new BalanceModel(balanceChanges, Chain);
            if(cancel.IsCancellationRequested)
            {
                if(balanceChanges.Count > 0)
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
        public async Task<object> WhatIsIt(string data)
        {
            WhatIsIt finder = new WhatIsIt(this);
            return (await finder.Find(data)) ?? "Good question Holmes !";
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
            if(block == null)
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
            if(hash == null)
                return null;
            var client = Configuration.Indexer.CreateIndexerClient();
            return headerOnly ? GetHeader(hash, client) : client.GetBlock(hash);
        }

        private Block GetHeader(uint256 hash, IndexerClient client)
        {
            var header = Chain.GetBlock(hash);
            if(header == null)
            {
                var b = client.GetBlock(hash);
                if(b == null)
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
