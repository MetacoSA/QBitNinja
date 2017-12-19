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
using System.IO;
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
		[Route("tx/{txId}")]
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
			bool colored = false,
			int? unconfExpiration = null)
		{
			var balanceId = new BalanceId(walletName);
			return Balance(balanceId, continuation, until, from, includeImmature, unspentOnly, colored, unconfExpiration);
		}

		[HttpPost]
		[Route("wallets/{walletname}/addresses")]
		public WalletAddress AddWalletAddresses(
			string walletName,
			[FromBody]InsertWalletAddress insertAddress)
		{
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
			address.UserData = insertAddress.UserData;
			address.WalletName = walletName;

			var repo = Configuration.CreateWalletRepository();

			if(!repo.AddWalletAddress(address, insertAddress.MergePast))
				throw Error(409, "This address already exist in the wallet");

			var unused = Configuration.Topics.AddedAddresses.AddAsync(new[] { address });
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

			KeySetData keysetData = new KeySetData
			{
				KeySet = keyset,
				State = new HDKeyState()
			};
			if(!repo.AddKeySet(walletName, keysetData))
				throw Error(409, "Keyset already exists");

			var newAddresses = repo.Scan(walletName, keysetData, 0, 20);

			foreach(var addresses in newAddresses.Partition(20))
			{
				var unused = Configuration.Topics.AddedAddresses.AddAsync(addresses.ToArray());
			}
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
		[HttpGet]
		[Route("wallets/{walletName}/keysets/{keysetName}")]
		public KeySetData GetKeyset(string walletName, string keysetName)
		{
			var repo = Configuration.CreateWalletRepository();
			return AssetWalletAndKeysetExists(walletName, keysetName);
		}

		[HttpGet]
		[Route("wallets/{walletName}/keysets/{keysetName}/unused/{lookahead}")]
		public HDKeyData GetUnused(string walletName, string keysetName, int lookahead)
		{
			if(lookahead < 0 || lookahead > 20)
				throw Error(400, "lookahead should be between 0 and 20");
			var keySet = AssetWalletAndKeysetExists(walletName, keysetName);
			var repo = Configuration.CreateWalletRepository();
			return keySet.GetUnused(lookahead);
		}

		private KeySetData AssetWalletAndKeysetExists(string walletName, string keysetName)
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
				return keyset;
			}
			return null;
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
			bool colored = false,
			int? unconfExpiration = null
			)
		{
			BalanceId id = new BalanceId(walletName);
			return BalanceSummary(id, at, debug, colored, unconfExpiration);
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
				ICoin coin = new Coin(new OutPoint(txId, i), txout);
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
			var confirmed = blockIds.Select(b => Chain.GetBlock(b)).Where(b => b != null).FirstOrDefault();
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
				MedianTimePast = confirmed.GetMedianTimePast(),
				BlockTime = confirmed.Header.BlockTime
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
			BlockFeature blockFeature, bool headerOnly = false, DataFormat format = DataFormat.Json, bool extended = false)
		{
			if(format == DataFormat.Json)
				return JsonBlock(blockFeature, headerOnly, extended);

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
		[Route("checkpoints")]
		public async Task<JArray> GetCheckpoints()
		{
			JArray arr = new JArray();
			var checkpoints = await this.Configuration.Indexer.CreateIndexer().GetCheckpointRepository().GetCheckpointsAsync();
			foreach(var check in checkpoints)
			{
				var jobj = new JObject();
				jobj.Add("Name", check.CheckpointName);
				jobj.Add("Height", Chain.FindFork(check.BlockLocator).Height);
				arr.Add(jobj);
			}
			return arr;
		}

		[HttpGet]
		[Route("balances/{balanceId}/summary")]
		public BalanceSummary AddressBalanceSummary(
			[ModelBinder(typeof(BalanceIdModelBinder))]
			BalanceId balanceId,
			[ModelBinder(typeof(BlockFeatureModelBinder))]
			BlockFeature at = null,
			bool debug = false,
			bool colored = false,
			int? unconfExpiration = null)
		{
			colored = colored || IsColoredAddress();
			return BalanceSummary(balanceId, at, debug, colored, unconfExpiration);
		}

		public BalanceSummary BalanceSummary(
			BalanceId balanceId,
			BlockFeature at,
			bool debug,
			bool colored,
			int? unconfExpiration)
		{
			var expiration = GetExpiration(unconfExpiration);
			var repo = Configuration.CreateWalletRepository();
			CancellationTokenSource cancel = new CancellationTokenSource();
			cancel.CancelAfter(30000);
			var checkpoint = Configuration.Indexer.CreateIndexer()
				.GetCheckpoint(balanceId.Type == BalanceType.Address ? IndexerCheckpoints.Balances : IndexerCheckpoints.Wallets);

			var atBlock = AtBlock(at);

			var query = new BalanceQuery();
			query.RawOrdering = true;
			query.From = null;

			if(at != null)
				query.From = ToBalanceLocator(atBlock);
			if(query.From == null)
				query.From = new UnconfirmedBalanceLocator(DateTimeOffset.UtcNow - expiration);

			query.PageSizes = new[] { 1, 10, 100 };

			var cacheTable = repo.GetBalanceSummaryCacheTable(balanceId, colored);
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
			int lookback = (int)(expiration.Ticks / this.Network.Consensus.PowTargetSpacing.Ticks);
			if(at == null)
				stopAtHeight = stopAtHeight - lookback;

			var client = Configuration.Indexer.CreateIndexerClient();
			client.ColoredBalance = colored;

			var diff =
				client
				.GetOrderedBalance(balanceId, query)
				.WhereNotExpired(expiration)
				.TakeWhile(_ => !cancel.IsCancellationRequested)
				//Some confirmation of the fetched unconfirmed may hide behind stopAtHeigh
				.TakeWhile(_ => _.BlockId == null || _.Height > stopAtHeight - lookback)
				.AsBalanceSheet(Chain);

			if(cancel.Token.IsCancellationRequested)
			{
				throw new HttpResponseException(new HttpResponseMessage()
				{
					StatusCode = HttpStatusCode.InternalServerError,
					ReasonPhrase = "The server can't fetch the balance summary because the balance is too big. Please, load it in several step with ?at={blockFeature} parameter. Once fully loaded after all the step, the summary will return in constant time."
				});
			}
			RemoveBehind(diff, stopAtHeight);
			RemoveConflicts(diff);

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

		private void RemoveBehind(BalanceSheet diff, int stopAtHeight)
		{
			RemoveBehind(diff.All, stopAtHeight);
			RemoveBehind(diff.Confirmed, stopAtHeight);
			RemoveBehind(diff.Unconfirmed, stopAtHeight);
			RemoveBehind(diff.Prunable, stopAtHeight);
		}

		private void RemoveBehind(List<OrderedBalanceChange> changes, int stopAtHeight)
		{
			foreach(var change in changes.ToList())
			{
				if(change.BlockId != null)
				{
					if(change.Height <= stopAtHeight)
						changes.Remove(change);
				}
			}
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
			return tip.Height - height >= Configuration.CoinbaseMaturity;
		}

		private bool IsMature(OrderedBalanceChange c, ChainedBlock tip)
		{
			return !c.IsCoinbase || (c.BlockId != null && IsMature(c.Height, tip));
		}

		[HttpGet]
		[Route("balances/{balanceId}")]
		public BalanceModel AddressBalance(
			[ModelBinder(typeof(BalanceIdModelBinder))]
			BalanceId balanceId,
			[ModelBinder(typeof(BalanceLocatorModelBinder))]
			BalanceLocator continuation = null,
			[ModelBinder(typeof(BlockFeatureModelBinder))]
			BlockFeature until = null,
			[ModelBinder(typeof(BlockFeatureModelBinder))]
			BlockFeature from = null,
			bool includeImmature = false,
			bool unspentOnly = false,
			bool colored = false,
			int? unconfExpiration = null)
		{
			colored = colored || IsColoredAddress();
			return Balance(balanceId, continuation, until, from, includeImmature, unspentOnly, colored, unconfExpiration);
		}

		//Property passed by BalanceIdModelBinder
		private bool IsColoredAddress()
		{
			return ActionContext.Request.Properties.ContainsKey("BitcoinColoredAddress");
		}

		TimeSpan Expiration = TimeSpan.FromHours(24.0);
		BalanceModel Balance(BalanceId balanceId,
			BalanceLocator continuation,
			BlockFeature until,
			BlockFeature from,
			bool includeImmature,
			bool unspentOnly,
			bool colored,
			int? unconfExpiration)
		{
			var expiration = GetExpiration(unconfExpiration);
			CancellationTokenSource cancel = new CancellationTokenSource();
			cancel.CancelAfter(30000);

			BalanceQuery query = new BalanceQuery();
			query.RawOrdering = true;
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
				query.RawOrdering = true;
			}

			if(query.From == null)
			{
				query.From = new UnconfirmedBalanceLocator(DateTimeOffset.UtcNow - expiration);
			}

			if(until != null)
			{
				query.To = ToBalanceLocator(until);
				query.FromIncluded = true;
			}

			if(query.To.IsGreaterThan(query.From))
				throw InvalidParameters("Invalid argument : from < until");

			var client = Configuration.Indexer.CreateIndexerClient();
			client.ColoredBalance = colored;
			var balance =
				client
				.GetOrderedBalance(balanceId, query)
				.TakeWhile(_ => !cancel.IsCancellationRequested)
				.WhereNotExpired(expiration)
				.Where(o => includeImmature || IsMature(o, Chain.Tip))
				.AsBalanceSheet(Chain);

			var balanceChanges = balance.All;

			if(until != null && balance.Confirmed.Count != 0) //Strip unconfirmed that can appear after the last until
			{
				List<OrderedBalanceChange> oldUnconfirmed = new List<OrderedBalanceChange>();
				var older = balanceChanges.Last();
				for(int i = 0; i < balanceChanges.Count; i++)
				{
					var last = balanceChanges[i];
					if(last.BlockId == null)
					{
						if(last.SeenUtc < older.SeenUtc)
							oldUnconfirmed.Add(last);
					}
					else
						break;
				}
				foreach(var unconf in oldUnconfirmed)
				{
					balanceChanges.Remove(unconf);
				}
			}

			var conflicts = RemoveConflicts(balance);

			if(unspentOnly)
			{
				HashSet<OutPoint> spents = new HashSet<OutPoint>();
				foreach(var change in balanceChanges.SelectMany(b => b.SpentCoins))
				{
					spents.Add(change.Outpoint);
				}
				foreach(var change in balanceChanges)
				{
					change.SpentCoins.Clear();
					change.ReceivedCoins.RemoveAll(c => spents.Contains(c.Outpoint));
				}
			}

			var result = new BalanceModel(balanceChanges, Chain);
			result.ConflictedOperations = result.GetBalanceOperations(conflicts, Chain);
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

		private TimeSpan GetExpiration(int? unconfExpiration)
		{
			return unconfExpiration == null ? Expiration : TimeSpan.FromHours(unconfExpiration.Value);
		}

		private List<OrderedBalanceChange> RemoveConflicts(BalanceSheet balance)
		{
			var spentOutputs = new Dictionary<OutPoint, OrderedBalanceChange>();
			var conflicts = new List<OrderedBalanceChange>();
			var unconfirmedConflicts = new List<OrderedBalanceChange>();
			foreach(var balanceChange in balance.All)
			{
				foreach(var spent in balanceChange.SpentCoins)
				{
					if(!spentOutputs.TryAdd(spent.Outpoint, balanceChange))
					{
						var balanceChange2 = spentOutputs[spent.Outpoint];
						var score = GetScore(balanceChange);
						var score2 = GetScore(balanceChange2);
						var conflicted =
							score == score2 ?
									((balanceChange.SeenUtc < balanceChange2.SeenUtc) ? balanceChange : balanceChange2) :
							score < score2 ? balanceChange : balanceChange2;
						conflicts.Add(conflicted);

						var nonConflicted = conflicted == balanceChange ? balanceChange2 : balanceChange;
						if(nonConflicted.BlockId == null || !Chain.Contains(nonConflicted.BlockId))
							unconfirmedConflicts.Add(conflicted);
					}
				}
			}
			foreach(var conflict in conflicts)
			{
				balance.All.Remove(conflict);
				balance.Unconfirmed.Remove(conflict);
			}

			return unconfirmedConflicts;
		}

		private long GetScore(OrderedBalanceChange balance)
		{
			long score = 0;
			if(balance.BlockId != null)
			{
				score += 10;
				if(Chain.Contains(balance.BlockId))
				{
					score += 100;
				}
			}
			return score;
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
		[Route("versionstats")]
		public VersionStatsResponse GetVersionStats()
		{
			VersionStatsResponse resp = new VersionStatsResponse();
			resp.Last144 = GetVersionStats(Chain.Tip.EnumerateToGenesis().Take(144).ToArray());
			resp.Last2016 = GetVersionStats(Chain.Tip.EnumerateToGenesis().Take(2016).ToArray());
			resp.SincePeriodStart = GetVersionStats(Chain.Tip.EnumerateToGenesis()
															 .TakeWhile(s => Chain.Tip == s || s.Height % Network.Consensus.DifficultyAdjustmentInterval != Network.Consensus.DifficultyAdjustmentInterval - 1).ToArray());
			return resp;
		}

		[HttpGet]
		[Route("bip9")]
		public JObject GetBIP9()
		{
			var stats = GetVersionStats();
			var result = JObject.Parse(JsonConvert.SerializeObject(stats, base.Configuration.Formatters.JsonFormatter.SerializerSettings));
			foreach(var period in result.OfType<JProperty>().Select(p => (JObject)p.Value))
			{
				foreach(var stat in ((JArray)period["stats"]).OfType<JObject>().ToArray())
				{
					((JArray)period["stats"]).Remove(stat);
					if(stat["proposal"] != null)
					{
						period.Add(stat["proposal"].ToString(), stat);
						stat.Remove("proposal");
					}
				}
				period.Remove("stats");
			}
			return result;
		}

		private VersionStats GetVersionStats(ChainedBlock[] chainedBlock)
		{
			VersionStats stats = new VersionStats();
			stats.Stats =
				chainedBlock
				.GroupBy(c => c.Header.Version)
				.Select(g => new VersionStatsItem()
				{
					Version = g.Key,
					Count = g.Count(),
					Percentage = ((double)g.Count() / (double)chainedBlock.Length) * 100.0
				}).ToList();


			var proposals = new[]
			{
				new
				{
					Name = "SEGWIT",
					Bit = 1
				}
			};

			foreach(var proposal in proposals)
			{
				var supportingBlocks =
					chainedBlock
					.Where(b => ((((uint)b.Header.Version & VERSIONBITS_TOP_MASK) == VERSIONBITS_TOP_BITS) && (b.Header.Version & ((uint)1) << proposal.Bit) != 0))
					.ToArray();
				stats.Stats.Add(new VersionStatsItem()
				{
					Proposal = proposal.Name,
					Count = supportingBlocks.Length,
					Percentage = ((double)supportingBlocks.Length / (double)chainedBlock.Length) * 100.0
				});
			}

			stats.FromHeight = chainedBlock.Last().Height;
			stats.ToHeight = chainedBlock.First().Height;
			stats.Total = chainedBlock.Length;
			return stats;
		}

		const ulong VERSIONBITS_TOP_BITS = 0x20000000UL;
		const ulong VERSIONBITS_TOP_MASK = 0xE0000000UL;

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

		internal GetBlockResponse JsonBlock(BlockFeature blockFeature, bool headerOnly, bool extended)
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
				ExtendedInformation = extended ? FetchExtendedBlockInformation(blockFeature, block) : null,
				Block = headerOnly ? null : block
			};
		}

		private ExtendedBlockInformation FetchExtendedBlockInformation(BlockFeature blockFeature, Block block)
		{
			var id = block.Header.GetHash().ToString();
			var extendedInfo = Configuration.GetCacheTable<ExtendedBlockInformation>().ReadOne(id);
			if(extendedInfo != null)
				return extendedInfo;
			ChainedBlock chainedBlock = blockFeature.GetChainedBlock(Chain);
			if(chainedBlock == null)
				return null;
			if(block.Transactions.Count == 0)
			{
				block = GetBlock(blockFeature, false);
				if(block == null || block.Transactions.Count == 0)
					return null;
			}
			extendedInfo = new ExtendedBlockInformation()
			{
				BlockReward = block.Transactions[0].TotalOut,
				BlockSubsidy = GetBlockSubsidy(chainedBlock.Height),
				Size = GetSize(block, TransactionOptions.All),
				StrippedSize = GetSize(block, TransactionOptions.None),
				TransactionCount = block.Transactions.Count
			};
			Configuration.GetCacheTable<ExtendedBlockInformation>().Create(id, extendedInfo, true);
			return extendedInfo;
		}

		public int GetSize(IBitcoinSerializable data, TransactionOptions options)
		{
			var bms = new BitcoinStream(Stream.Null, true);
			bms.TransactionOptions = options;
			data.ReadWrite(bms);
			return (int)bms.Counter.WrittenBytes;
		}
		Money GetBlockSubsidy(int nHeight)
		{
			int halvings = nHeight / Configuration.Indexer.Network.Consensus.SubsidyHalvingInterval;
			// Force block reward to zero when right shift is undefined.
			if(halvings >= 64)
				return 0;

			Money nSubsidy = Money.Coins(50);
			// Subsidy is cut in half every 210,000 blocks which will occur approximately every 4 years.
			nSubsidy >>= halvings;
			return nSubsidy;
		}

		private Block GetBlock(BlockFeature blockFeature, bool headerOnly)
		{
			var chainedBlock = blockFeature.GetChainedBlock(Chain);
			var hash = chainedBlock == null ? blockFeature.BlockId : chainedBlock.HashBlock;
			if(hash == null)
				return null;
			if(chainedBlock != null && chainedBlock.Height == 0)
				return headerOnly ? new NBitcoin.Block(Network.GetGenesis().Header) : Network.GetGenesis();
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
