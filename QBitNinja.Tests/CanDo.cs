using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer;
using NBitcoin.OpenAsset;
using NBitcoin.Policy;
using NBitcoin.Protocol;
using Newtonsoft.Json.Linq;
using QBitNinja.Controllers;
using QBitNinja.Models;
using QBitNinja.Notifications;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace QBitNinja.Tests
{
    public class CanDo
    {
        [Fact]
        public void CanGetTransaction()
        {
            using(var tester = ServerTester.Create())
            {
                var bob = new Key();
                var alice = new Key();

                //Not found should return 404 (Not found)
                var txId = uint256.Parse(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)));
                uint256 id = txId;
                AssertEx.HttpError(404, () => tester.SendGet<GetTransactionResponse>("transactions/" + id));     // todo: there's a risk that Dispose() will be called for tester when the lambda executes
                ////

                //Not correctly formatted should return 400 (Bad request)
                AssertEx.HttpError(400, () => tester.SendGet<GetTransactionResponse>("transactions/000lol"));    // todo: there's a risk that Dispose() will be called for tester when the lambda executes
                ////

                //Found should return the correct transaction
                var tx = tester.ChainBuilder.EmitMoney("1.0", bob);
                txId = tx.GetHash();
                var response = tester.SendGet<GetTransactionResponse>("transactions/" + txId);
                Assert.NotNull(response);
                Assert.Equal(txId.ToString(), response.TransactionId.ToString());
                Assert.Equal(tx.ToString(), response.Transaction.ToString());
                ////

                //Previously spent coins should be in the response
                var prevTx = tx;
                TransactionBuilder txBuilder = new TransactionBuilder();
                tx =
                    txBuilder
                    .AddKeys(bob)
                    .AddCoins(new Coin(tx, 0))
                    .Send(alice, "0.99")
                    .SendFees("0.01")
                    .BuildTransaction(true);
                tester.ChainBuilder.Broadcast(tx);
                txId = tx.GetHash();
                response = tester.SendGet<GetTransactionResponse>("transactions/" + txId);
                var firstSeen = response.FirstSeen;
                Assert.True(DateTimeOffset.UtcNow - firstSeen < TimeSpan.FromMinutes(1.0));
                Assert.Equal(txId.ToString(), response.TransactionId.ToString());
                Assert.Equal(tx.ToString(), response.Transaction.ToString());
                Assert.Null(response.Block);
                Assert.Equal(Money.Parse("0.01"), response.Fees);
                Assert.Equal(prevTx.GetHash(), response.SpentCoins[0].Outpoint.Hash);
                Assert.Equal(0U, response.SpentCoins[0].Outpoint.N);
                Assert.Equal(Money.Parse("1.00"), response.SpentCoins[0].TxOut.Value);
                Assert.Equal(bob.ScriptPubKey, response.SpentCoins[0].TxOut.ScriptPubKey);
                ////

                //Once mined, the transaction should have block information
                var block = tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                response = tester.SendGet<GetTransactionResponse>("transactions/" + txId);
                Assert.Equal(firstSeen, response.FirstSeen);
                Assert.NotNull(response.Block);
                Assert.True(response.Block.BlockId == block.GetHash());
                Assert.True(response.Block.Confirmations == 1);
                Assert.True(response.Block.Height == 1);

                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                response = tester.SendGet<GetTransactionResponse>("transactions/" + txId);
                Assert.NotNull(response.Block);
                Assert.True(response.Block.BlockId == block.GetHash());
                Assert.True(response.Block.BlockHeader.GetHash() == block.Header.GetHash());
                Assert.True(response.Block.Confirmations == 2);
                Assert.True(response.Block.Height == 1);
                var json = Serializer.ToString(response); //Can serialize without blowing up
                Assert.True(json.Contains(txId.ToString())); //Just ensure no big endian problem for id
                ////

                //Can get it raw
                var bytes = tester.SendGet<byte[]>("transactions/" + tx.GetHash() + "?format=raw");
                Assert.True(bytes.SequenceEqual(tx.ToBytes()));
                response = tester.SendGet<GetTransactionResponse>("transactions/" + txId + "?format=json");
                /////

                //Colored version
                var goldGuy = new Key();
                var silverGuy = new Key();
                var gold = new AssetId(goldGuy);
                var silver = new AssetId(silverGuy);

                tx = tester.ChainBuilder.EmitMoney(Money.Coins(200), bob);
                tx = tester.ChainBuilder.EmitMoney(Money.Coins(100), goldGuy);
                var goldIssuance = new IssuanceCoin(tx.Outputs.AsCoins().First());
                tx = tester.ChainBuilder.EmitMoney(Money.Coins(95), silverGuy);
                var silverIssuance = new IssuanceCoin(tx.Outputs.AsCoins().First());

                tx = new TransactionBuilder()
                {
                    StandardTransactionPolicy = Policy
                }
                     .AddKeys(goldGuy)
                     .AddCoins(goldIssuance)
                     .IssueAsset(bob, new AssetMoney(gold, 1000))
                     .SetChange(goldGuy)
                     .Then()
                     .AddKeys(bob)
                     .AddCoins(tester.GetUnspentCoins(bob))
                     .Send(goldGuy, Money.Coins(5.0m))
                     .Send(silverGuy, Money.Coins(31.0m))
                     .SetChange(bob)
                     .Shuffle()
                     .BuildTransaction(true);
                tester.ChainBuilder.Broadcast(tx);

                response = tester.SendGet<GetTransactionResponse>("transactions/" + tx.GetHash() + "?colored=true");
                //Bob receives 1000 gold
                Assert.True(response.ReceivedCoins.OfType<ColoredCoin>().Any(c => c.Amount == new AssetMoney(gold, 1000) && c.TxOut.ScriptPubKey == bob.ScriptPubKey));

                //Goldguy receives 5BTC
                Assert.True(response.ReceivedCoins.OfType<Coin>().Any(c => c.Amount == Money.Coins(5.0m) && c.TxOut.ScriptPubKey == goldGuy.ScriptPubKey));

                //SilverGuy receives 31BTC
                Assert.True(response.ReceivedCoins.OfType<Coin>().Any(c => c.Amount == Money.Coins(31.0m) && c.TxOut.ScriptPubKey == silverGuy.ScriptPubKey));

                tx = new TransactionBuilder()
                {
                    StandardTransactionPolicy = Policy
                }
                     .AddKeys(silverGuy)
                     .AddCoins(silverIssuance)
                     .IssueAsset(bob, new AssetMoney(silver, 1500))
                     .SetChange(goldGuy)
                     .Then()
                     .AddKeys(bob)
                     .AddCoins(tester.GetUnspentCoins(bob))
                     .Send(silverGuy, Money.Coins(37.0m))
                     .Send(silverGuy, new AssetMoney(gold, 600))
                     .SetChange(bob)
                     .Shuffle()
                     .BuildTransaction(true);
                tester.ChainBuilder.Broadcast(tx);

                response = tester.SendGet<GetTransactionResponse>("transactions/" + tx.GetHash() + "?colored=true");
                //Bob spent previous 1000 gold
                Assert.True(response.SpentCoins.OfType<ColoredCoin>().Any(c => c.Amount == new AssetMoney(gold, 1000) && c.TxOut.ScriptPubKey == bob.ScriptPubKey));

                //Bob received 1500 silver
                Assert.True(response.ReceivedCoins.OfType<ColoredCoin>().Any(c => c.Amount == new AssetMoney(silver, 1500) && c.TxOut.ScriptPubKey == bob.ScriptPubKey));

                //Silverguy received 600 gold
                Assert.True(response.ReceivedCoins.OfType<ColoredCoin>().Any(c => c.Amount == new AssetMoney(gold, 600) && c.TxOut.ScriptPubKey == silverGuy.ScriptPubKey));
                //Bob received 400 gold
                Assert.True(response.ReceivedCoins.OfType<ColoredCoin>().Any(c => c.Amount == new AssetMoney(gold, 400) && c.TxOut.ScriptPubKey == bob.ScriptPubKey));

                //Silverguy received 37BTC
                Assert.True(response.ReceivedCoins.OfType<Coin>().Any(c => c.Amount == Money.Coins(37.0m) && c.TxOut.ScriptPubKey == silverGuy.ScriptPubKey));
            }
        }

        [Fact]
        public void CanInitialIndex()
        {
            using(var tester = ServerTester.Create())
            {
                var bob = new Key();
                var listener = tester.CreateListenerTester(true);
                tester.ChainBuilder.EmitBlock(); //1
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitMoney(Money.Coins(1.0m), bob);
                tester.ChainBuilder.EmitMoney(Money.Coins(1.1m), bob);
                tester.ChainBuilder.EmitBlock(); // 5
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock(); //10
                tester.ChainBuilder.EmitMoney(Money.Coins(1.2m), bob);
                var last = tester.ChainBuilder.EmitBlock(); //11

                InitialIndexer indexer = new InitialIndexer(tester.Configuration);
                indexer.BlockGranularity = 5;
                indexer.TransactionsPerWork = 11;
                Assert.Equal(5 * 2, indexer.Run());

                var client = tester.Configuration.Indexer.CreateIndexerClient();
                Assert.Equal(3, client.GetOrderedBalance(bob).Count());

                Assert.Equal(last.GetHash(), tester.Configuration.Indexer.CreateIndexer().GetCheckpoint(IndexerCheckpoints.Balances).BlockLocator.Blocks[0]);

                indexer.Cancel();
            }
        }

        [Fact]
        public void CanInitialIndexConcurrently()
        {
            using(var tester = ServerTester.Create())
            {
                var bob = new Key();
                var listener = tester.CreateListenerTester(true);
                tester.ChainBuilder.EmitBlock(); //1
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitMoney(Money.Coins(1.0m), bob);
                tester.ChainBuilder.EmitMoney(Money.Coins(1.1m), bob);
                tester.ChainBuilder.EmitBlock(); // 5
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock(); //10
                tester.ChainBuilder.EmitMoney(Money.Coins(1.2m), bob);
                tester.ChainBuilder.EmitBlock(); //11

                var processed = Enumerable
                    .Range(0, 10)
                    .Select(_ => Task.Run(() =>
                    {
                        InitialIndexer indexer = new InitialIndexer(tester.Configuration);
                        indexer.BlockGranularity = 5;
                        indexer.TransactionsPerWork = 11;
                        return indexer.Run();
                    }))
                    .Select(s => s.Result)
                    .Sum();

                Assert.Equal(5 * 2, processed);

                var client = tester.Configuration.Indexer.CreateIndexerClient();
                Assert.Equal(3, client.GetOrderedBalance(bob).Count());
            }
        }

        //[Fact]
        public void Play()
        {
            using(var fs = File.Open("c:/blocksize.csv", FileMode.Create))
            {
                StreamWriter writer = new StreamWriter(fs);
                var node = Node.ConnectToLocal(Network.Main);
                node.VersionHandshake();
                var chain = node.GetChain();

                foreach(var block in node.GetBlocks(chain.EnumerateAfter(chain.Genesis).Where(c => c.Height % 100 == 0).Select(c => c.HashBlock)))
                {
                    var blockinfo = chain.GetBlock(block.GetHash());
                    writer.WriteLine(blockinfo.Height + "," + block.Transactions.Count);
                }
                writer.Flush();
            }
			//var conf = IndexerConfiguration.FromConfiguration();
			//var client = conf.CreateIndexerClient();
			//var rules = client.GetAllWalletRules();
			//var result = rules.GroupBy(r => r.WalletId);

			//var indexer = conf.CreateIndexer();

			//var b = client.GetBlock(new uint256("..."));
			//indexer.IndexWalletOrderedBalance(0, b, rules);

			//using (var tester = ServerTester.Create())
			//{
			//    var walletName = System.Web.NBitcoin.HttpUtility.UrlEncode("@098098.@##.balance?frpoeifpo")
			//        .Replace("/", "%2F")
			//        .Replace("?", "%3F");

			//    tester.Send<string>(HttpMethod.Post, "wallets", new WalletModel()
			//    {
			//        Name = "@098098.//frpoeifpo"
			//    });
			//    tester.SendGet<string>("wallets/" + walletName);
			//}
		}

		[Fact]
		public void CanGetVersionStats()
		{
			using(var tester = ServerTester.Create())
			{
				tester.ChainBuilder.EmitBlock();
				tester.ChainBuilder.EmitBlock();
				tester.ChainBuilder.EmitBlock();
				tester.ChainBuilder.EmitBlock(blockVersion: 536870913);
				var response = tester.Send<VersionStatsResponse>(HttpMethod.Get, "versionstats");
			}
		}


		[Fact]
        public void CanBroadcastTransaction()
        {
            using(var tester = ServerTester.Create())
            {
                var tx = CreateRandomTx();
                var bytes = Encoders.Hex.EncodeData(tx.ToBytes());
                var listener = tester.CreateListenerTester();
                var response = tester.Send<BroadcastResponse>(HttpMethod.Post, "transactions", bytes);
                Assert.True(response.Success);
                Assert.True(response.Error == null);
                listener.AssertReceivedTransaction(tx.GetHash());

                listener.Reject(new RejectPayload()
                {
                    Code = RejectCode.INSUFFICIENTFEE
                });

                tx = CreateRandomTx();
                bytes = Encoders.Hex.EncodeData(tx.ToBytes());
                response = tester.Send<BroadcastResponse>(HttpMethod.Post, "transactions", bytes);

                Assert.False(response.Success);
                Assert.True(response.Error.ErrorCode == RejectCode.INSUFFICIENTFEE);
            }
        }

        private Transaction CreateRandomTx()
        {
            Transaction tx = new Transaction();
            tx.Inputs.Add(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(RandomUtils.GetBytes(32)))
            });
            return tx;
        }

        [Fact]
        public void CanReceiveTransactionNotification()
        {
            using(var tester = ServerTester.Create())
            {
                var notifications = tester.CreateNotificationServer();
                tester.Send<string>(HttpMethod.Post, "subscriptions", new NewTransactionSubscription()
                {
                    Id = "toto",
                    Url = notifications.Address
                });
            }
        }


        [Fact]
        public void CanReceiveNewTransactionNotification()
        {
            using(var tester = ServerTester.Create())
            {
                var notifications = tester.CreateNotificationServer();
                tester.Send<string>(HttpMethod.Post, "subscriptions", new NewTransactionSubscription()
                {
                    Id = "toto",
                    Url = notifications.Address
                });

                var listener = tester.CreateListenerTester();

                var bob = new Key();
                var tx = tester.ChainBuilder.EmitMoney(Money.Coins(1.0m), bob);

                var request = notifications.WaitRequest();
                request.Complete(true);
                var data = (NewTransactionNotificationData)request.GetBody<Notification>().Data;
                Assert.True(data.TransactionId == tx.GetHash());
            }
        }
        [Fact]
        public void CanReceiveNewBlockNotification()
        {
            using(var tester = ServerTester.Create())
            {
                var notifications = tester.CreateNotificationServer();
                tester.Send<string>(HttpMethod.Post, "subscriptions", new NewBlockSubscription()
                {
                    Id = "toto",
                    Url = notifications.Address
                });

                var listener = tester.CreateListenerTester();
                var b = tester.ChainBuilder.EmitBlock();
                var request = notifications.WaitRequest();
                request.Complete(true);

                var notification = request.GetBody<Notification>();
                var data = (NewBlockNotificationData)notification.Data;
                Assert.True(notification.Subscription.Id == "toto");
                Assert.True(data.BlockId == b.Header.HashPrevBlock);
                Assert.True(data.Height == 0);
                Assert.True(data.Header.GetHash() == b.Header.HashPrevBlock);

                request = notifications.WaitRequest();
                request.Complete(true);
                notification = request.GetBody<Notification>();
                data = (NewBlockNotificationData)notification.Data;
                Assert.True(notification.Subscription.Id == "toto");
                Assert.True(data.BlockId == b.Header.GetHash());
                Assert.True(data.Height == 1);
                Assert.True(notification.Tried == 1);
                Assert.True(data.Header.GetHash() == b.Header.GetHash());

                //Fail
                b = tester.ChainBuilder.EmitBlock();
                request = notifications.WaitRequest();
                request.Complete(false);
                notification = request.GetBody<Notification>();
                data = (NewBlockNotificationData)notification.Data;
                Assert.True(data.Header.GetHash() == b.Header.GetHash());

                //Check has retried
                request = notifications.WaitRequest();
                request.Complete(true);
                notification = request.GetBody<Notification>();
                data = (NewBlockNotificationData)notification.Data;
                Assert.True(data.Header.GetHash() == b.Header.GetHash());
                Assert.True(notification.Tried == 2);
            }
        }

        [Fact]
        public void CanListenBlockchain()
        {
            using(var tester = ServerTester.Create())
            {
                var listener = tester.CreateListenerTester();
                var indexer = tester.Configuration.Indexer.CreateIndexerClient();

                var bob = new Key().GetBitcoinSecret(Network.TestNet);


                var wait = listener.WaitMessageAsync(tester.Configuration.Topics.NewTransactions);
                var tx = tester.ChainBuilder.EmitMoney(Money.Coins(1.0m), bob);
                wait.Wait();

                var balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress());
                Assert.True(balance.Operations.Count == 1);
                Assert.True(balance.Operations[0].Confirmations == 0);
                Assert.True(balance.Operations[0].BlockId == null);

                var savedTx = tester.SendGet<GetTransactionResponse>("transactions/" + tx.GetHash());
                Assert.NotNull(savedTx);
                Assert.True(savedTx.Block == null);


                var waitBlock = listener.WaitMessageAsync(tester.Configuration.Topics.NewBlocks);
                var block = tester.ChainBuilder.EmitBlock();
                waitBlock.Wait();

                Assert.NotNull(indexer.GetBlock(block.GetHash()));
                tester.UpdateServerChain(true);

                balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress());
                Assert.True(balance.Operations.Count == 1);
                Assert.True(balance.Operations[0].Confirmations == 1);
                Assert.True(balance.Operations[0].BlockId == block.GetHash());

                savedTx = tester.SendGet<GetTransactionResponse>("transactions/" + tx.GetHash());
                Assert.NotNull(savedTx);
                Assert.True(savedTx.Block.Confirmations == 1);

            }
        }

        void Insist(Action act)
        {
            int tried = 0;
            while(true)
            {
                try
                {
                    act();
                    break;
                }
                catch(Exception)
                {
                    tried++;
                    if(tried > 5)
                        throw;
                    Thread.Sleep(1000);
                }
            }
        }

        [Fact]
        public void CanRecreateSubscriptionsAndTopics()
        {
            using(var tester = ServerTester.Create())
            {
                var queue = new QBitNinjaTopic<Transaction>(tester.Configuration.ServiceBus, "toto");
                queue.EnsureExistsAsync().Wait();
                queue.CreateConsumer(new SubscriptionCreation()).EnsureSubscriptionExists();
                queue.CreateConsumer(new SubscriptionCreation()
                {
                    UserMetadata = "hello"
                }).EnsureSubscriptionExists();

                queue = new QBitNinjaTopic<Transaction>(tester.Configuration.ServiceBus, new TopicCreation("toto")
                {
                    EnableExpress = true
                });
                queue.EnsureExistsAsync().Wait();
            }
        }

        [Fact]
        public void CanGetBlock()
        {
            using(var tester = ServerTester.Create())
            {
                tester.ChainBuilder.UploadBlock = true;

                var bob = new Key();
                var tx = tester.ChainBuilder.EmitMoney("1.0", bob);
                var block = tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();
                var response = tester.SendGet<byte[]>("blocks/" + block.GetHash() + "?format=raw");
                Assert.True(response.SequenceEqual(block.ToBytes()));

                //404 if not found
                AssertEx.HttpError(404, () => tester.SendGet<byte[]>("blocks/18179931ea977cc0030c7c3e3e4d457f384b9e00aee9d86e39fbff0c5d3f4c40?format=raw")); // todo: there's a risk that Dispose() will be called for tester when the lambda executes
                /////

                //Can get Block with additional data
                var response2 = tester.SendGet<GetBlockResponse>("blocks/" + block.GetHash());
                Assert.True(response2.Block.ToBytes().SequenceEqual(block.ToBytes()));
                Assert.True(response2.AdditionalInformation.BlockHeader.GetHash() == block.Header.GetHash());
                Assert.True(response2.AdditionalInformation.BlockId == block.Header.GetHash());
                Assert.True(response2.AdditionalInformation.Confirmations == 1);
                Assert.True(response2.AdditionalInformation.Height == 1);
                /////

                //Can get Block by height and special
                var response3 = tester.SendGet<GetBlockResponse>("blocks/1");
                Assert.Equal(Serializer.ToString(response2), Serializer.ToString(response3));
                response3 = tester.SendGet<GetBlockResponse>("blocks/last");
                Assert.Equal(Serializer.ToString(response2), Serializer.ToString(response3));
                ////

                //Can get header only
                response3 = tester.SendGet<GetBlockResponse>("blocks/1?headerOnly=true&format=json");
                Assert.Null(response3.Block);
                Assert.NotNull(response3.AdditionalInformation);

                response = tester.SendGet<byte[]>("blocks/1?headerOnly=true&format=raw");
                Assert.True(response.SequenceEqual(response3.AdditionalInformation.BlockHeader.ToBytes()));
                ////

                //Check the blockFeature
                var block2 = tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                response2 = tester.SendGet<GetBlockResponse>("blocks/tip-1");
                Assert.True(response2.Block.ToBytes().SequenceEqual(block.ToBytes()));

                response2 = tester.SendGet<GetBlockResponse>("blocks/tip");
                Assert.True(response2.Block.ToBytes().SequenceEqual(block2.ToBytes()));

                response2 = tester.SendGet<GetBlockResponse>("blocks/tip-10?headerOnly=true");
                Assert.True(response2.AdditionalInformation.BlockHeader.ToBytes().SequenceEqual(Network.TestNet.GetGenesis().Header.ToBytes()));

                AssertEx.HttpError(404, () => tester.SendGet<byte[]>("blocks/tip+1?format=raw"));

                response2 = tester.SendGet<GetBlockResponse>("blocks/" + block.Header.GetHash() + "+1");
                Assert.True(response2.Block.ToBytes().SequenceEqual(block2.ToBytes()));
                /////
            }
        }

        public class TestData
        {
            public string Name
            {
                get;
                set;
            }
        }

        [Fact]
        public void CanUseCrudTable()
        {
            using(var tester = ServerTester.Create())
            {
                var facto = tester.Configuration.GetCrudTableFactory(new Scope(new[] { "test" }));
                var table = facto.GetTable<TestData>("testdata");

                table.Create("child1", new TestData());

                var childTable = table.GetChild("child1");
                childTable.Create("test", new TestData());

                Assert.NotNull(table.ReadOne("child1"));
                Assert.True(table.Read().Length == 1);

                table.Delete("child1");
                Assert.True(table.Read().Length == 0);

                Assert.True(childTable.Read().Length == 1);

                table.Create("child1", new TestData());
                table.Delete("child1", true);
                Assert.True(childTable.Read().Length == 0);

                childTable.Create("test", new TestData());
                childTable.Create("test2", new TestData());
                Assert.True(childTable.Read().Length == 2);
                childTable.Delete();
                Assert.True(childTable.Read().Length == 0);
            }
        }

        [Fact]
        public void CanUseCacheTable()
        {
            using(var tester = ServerTester.Create())
            {
                var table = tester.Configuration.GetChainCacheTable<string>(new Scope(new[] { "test" }));
                var a1 = tester.ChainBuilder.AddToChain();
                table.Create(GetLocator(tester.ChainBuilder), "a1");
                var a2 = tester.ChainBuilder.AddToChain();
                var a3 = tester.ChainBuilder.AddToChain();
                table.Create(GetLocator(tester.ChainBuilder), "a3");

                var result = table.Query(tester.ChainBuilder.Chain, new BalanceQuery()
                {
                    From = GetLocator(a3),
                    To = GetLocator(a1),
                });
                AssertIs("a3,a1", result);

                tester.ChainBuilder.SetTip(a1.Header);
                var b2 = tester.ChainBuilder.AddToChain();
                table.Create(GetLocator(tester.ChainBuilder), "b2");

                result = table.Query(tester.ChainBuilder.Chain, new BalanceQuery()
                {
                    To = GetLocator(a1),
                });
                AssertIs("b2,a1", result);
            }
        }

        private void AssertIs(string expected, System.Collections.Generic.IEnumerable<string> actual)
        {
            Assert.Equal(expected, String.Join(",", actual.ToArray()));
        }

        private ConfirmedBalanceLocator GetLocator(ChainedBlock chainedBlock)
        {
            return new ConfirmedBalanceLocator(chainedBlock.Height, chainedBlock.HashBlock);
        }

        private ConfirmedBalanceLocator GetLocator(ChainBuilder chainBuilder)
        {
            return GetLocator(chainBuilder.Chain.Tip);
        }


        [Fact]
        public void CanGetBalanceSummary3()
        {
            using(var tester = ServerTester.Create())
            {
                tester.Configuration.CoinbaseMaturity = 5;
                //Alice hit an invalid cached summary
                var bob = new Key().GetBitcoinSecret(Network.TestNet);
                var alice = new Key().GetBitcoinSecret(Network.TestNet);

                tester.ChainBuilder.EmitMoney(Money.Parse("0.9"), alice, coinbase: true);
                var firstCoinbase = tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                var result = tester.SendGet<BalanceSummary>("balances/" + alice.GetAddress() + "/summary?debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.9"),
                        Amount = Money.Parse("0.9")
                    },
                    UnConfirmed = new BalanceSummaryDetails(),
                    Immature = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.9"),
                        Amount = Money.Parse("0.9")
                    },
                    Spendable = new BalanceSummaryDetails(),
                    CacheHit = CacheHit.NoCache
                }, result);

                tester.ChainBuilder.EmitMoney(Money.Parse("0.8"), alice, coinbase: true);
                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                //2 conf for coinbase, nothing changed for alice
                result = tester.SendGet<BalanceSummary>("balances/" + alice.GetAddress() + "/summary?debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("1.7"),
                        Amount = Money.Parse("1.7")
                    },
                    UnConfirmed = new BalanceSummaryDetails(),
                    Immature = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("1.7"),
                        Amount = Money.Parse("1.7")
                    },
                    Spendable = new BalanceSummaryDetails(),
                    CacheHit = CacheHit.PartialCache
                }, result);

                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                //5 conf for coinbase, it is mature, and so the cache should not be hit.
                result = tester.SendGet<BalanceSummary>("balances/" + alice.GetAddress() + "/summary?debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("1.7"),
                        Amount = Money.Parse("1.7")
                    },
                    UnConfirmed = new BalanceSummaryDetails(),
                    Immature = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.8"),
                        Amount = Money.Parse("0.8")
                    },
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.9"),
                        Amount = Money.Parse("0.9")
                    },
                    CacheHit = CacheHit.NoCache //The previous cache have expired immature, so it miss
                }, result);

                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                result = tester.SendGet<BalanceSummary>("balances/" + alice.GetAddress() + "/summary?debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("1.7"),
                        Amount = Money.Parse("1.7")
                    },
                    UnConfirmed = new BalanceSummaryDetails(),
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("1.7"),
                        Amount = Money.Parse("1.7")
                    },
                    CacheHit = CacheHit.NoCache //The previous cache have expired immature, so it miss
                }, result);

                var tx = tester.ChainBuilder.EmitMoney(Money.Parse("0.1"), alice, coinbase: true);
                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                tester.ChainBuilder.SendMoney(alice, bob, tx, Money.Parse("0.04"));

                result = tester.SendGet<BalanceSummary>("balances/" + alice.GetAddress() + "/summary?debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 3,
                        Received = Money.Parse("1.8"),
                        Amount = Money.Parse("1.8")
                    },
                    UnConfirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0"),
                        Amount = -Money.Parse("0.04")
                    },
                    Immature = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.1"),
                        Amount = Money.Parse("0.1")
                    },
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 3,
                        Received = Money.Parse("1.7"),
                        Amount = Money.Parse("1.66")
                    },
                    CacheHit = CacheHit.PartialCache //The previous cache have not expired immature, so it should not miss
                }, result);

                //Fork happen, making loose the second coinbase of Alice, and the remaining unconfirmed will not be in this new chain
                tester.ChainBuilder.ClearMempool();
                tester.ChainBuilder.SetTip(firstCoinbase.Header);
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitMoney(Money.Parse("0.21"), alice, coinbase: true);
                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                result = tester.SendGet<BalanceSummary>("balances/" + alice.GetAddress() + "/summary?debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0"),
                        Amount = -Money.Parse("0.04")
                    },
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("1.11"),
                        Amount = Money.Parse("1.11")
                    },
                    Immature = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Amount = Money.Parse("0.21"),
                        Received = Money.Parse("0.21"),
                    },
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Amount = Money.Parse("0.86"),
                        Received = Money.Parse("0.9")
                    },
                    CacheHit = CacheHit.NoCache //The previous cache have not expired because coinbase 1 matured
                }, result);

                tester.ChainBuilder.EmitBlock();

                result = tester.SendGet<BalanceSummary>("balances/" + alice.GetAddress() + "/summary?debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0"),
                        Amount = -Money.Parse("0.04")
                    },
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("1.11"),
                        Amount = Money.Parse("1.11")
                    },
                    Immature = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Amount = Money.Parse("0.21"),
                        Received = Money.Parse("0.21"),
                    },
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Amount = Money.Parse("0.86"),
                        Received = Money.Parse("0.9")
                    },
                    CacheHit = CacheHit.PartialCache //The previous cache have not expired (where there is coinbase 2)
                }, result);

            }
        }

        [Fact]
        public void CanGetBalanceSummary2()
        {
            using(var tester = ServerTester.Create())
            {
                tester.Configuration.CoinbaseMaturity = 5;
                var bob = new Key().GetBitcoinSecret(Network.TestNet);
                tester.ChainBuilder.EmitMoney("0.01", bob, coinbase: false);
                tester.ChainBuilder.EmitMoney("0.1", bob, coinbase: true);
                var result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary?debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.01"),
                        Amount = Money.Parse("0.01")
                    },
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.01"),
                        Amount = Money.Parse("0.01")
                    },
                    CacheHit = CacheHit.NoCache
                }, result);

                //The at should remove any unconfirmed info
                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary?at=0&debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails(),
                    CacheHit = CacheHit.FullCache
                }, result);

                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                //Coinbase 1 confirmed
                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary?debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = new BalanceSummaryDetails(),
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("0.11"),
                        Amount = Money.Parse("0.11")
                    },
                    Immature = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.1"),
                        Amount = Money.Parse("0.1")
                    },
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.01"),
                        Amount = Money.Parse("0.01")
                    },
                    CacheHit = CacheHit.PartialCache
                }, result);

                //Same query with at
                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary?at=1&debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = null,
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("0.11"),
                        Amount = Money.Parse("0.11")
                    },
                    Immature = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.1"),
                        Amount = Money.Parse("0.1")
                    },
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.01"),
                        Amount = Money.Parse("0.01")
                    },
                    CacheHit = CacheHit.FullCache
                }, result);

                tester.ChainBuilder.EmitMoney("0.21", bob, coinbase: true);
                tester.ChainBuilder.EmitMoney("0.3", bob);

                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary?debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.3"),
                        Amount = Money.Parse("0.3"),
                    },
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("0.11"),
                        Amount = Money.Parse("0.11")
                    },
                    Immature = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.1"),
                        Amount = Money.Parse("0.1"),
                    },
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("0.01") + Money.Parse("0.3"),
                        Amount = Money.Parse("0.01") + Money.Parse("0.3")
                    },
                    CacheHit = CacheHit.PartialCache
                }, result);

                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary?debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = new BalanceSummaryDetails(),
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 4,
                        Received = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21"),
                        Amount = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21")
                    },
                    Immature = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("0.21") + Money.Parse("0.1"),
                        Amount = Money.Parse("0.21") + Money.Parse("0.1"),
                    },
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("0.01") + Money.Parse("0.3"),
                        Amount = Money.Parse("0.01") + Money.Parse("0.3"),
                    },
                    CacheHit = CacheHit.PartialCache
                }, result);

                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");
                Assert.True(result.Spendable.Amount == Money.Parse("0.31"));

                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                var firstMaturityHeight = tester.ChainBuilder.Chain.Height;

                //First coinbase is now mature
                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary?debug=true");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = new BalanceSummaryDetails(),
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 4,
                        Received = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21"),
                        Amount = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21")
                    },
                    Immature = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.21"),
                        Amount = Money.Parse("0.21"),
                    },
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 3,
                        Received = Money.Parse("0.01") + Money.Parse("0.3") + Money.Parse("0.1"),
                        Amount = Money.Parse("0.01") + Money.Parse("0.3") + Money.Parse("0.1"),
                    },
                    CacheHit = CacheHit.PartialCache
                }, result);

                //Did not altered history
                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary?at=" + (tester.ChainBuilder.Chain.Height - 1));
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = null,
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 4,
                        Received = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21"),
                        Amount = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21")
                    },
                    Immature = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("0.21") + Money.Parse("0.1"),
                        Amount = Money.Parse("0.21") + Money.Parse("0.1"),
                    },
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("0.01") + Money.Parse("0.3"),
                        Amount = Money.Parse("0.01") + Money.Parse("0.3"),
                    }
                }, result);

                //Second coin base
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = new BalanceSummaryDetails(),
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 4,
                        Received = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21"),
                        Amount = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21")
                    },
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 4,
                        Received = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21"),
                        Amount = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21"),
                    }
                }, result);
                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary?debug=true&at=" + (2 + 4));
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = null,
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 4,
                        Received = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21"),
                        Amount = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21")
                    },
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 4,
                        Received = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21"),
                        Amount = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21"),
                    },
                    CacheHit = CacheHit.PartialCache
                }, result);

                //Did not altered history when the first coinbase was confirmed
                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary?at=" + firstMaturityHeight);
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = null,
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 4,
                        Received = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21"),
                        Amount = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21")
                    },
                    Immature = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0.21"),
                        Amount = Money.Parse("0.21"),
                    },
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 3,
                        Received = Money.Parse("0.01") + Money.Parse("0.3") + Money.Parse("0.1"),
                        Amount = Money.Parse("0.01") + Money.Parse("0.3") + Money.Parse("0.1"),
                    }
                }, result);

                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary?at=" + (firstMaturityHeight + 1));
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = null,
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 4,
                        Received = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21"),
                        Amount = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21")
                    },
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 4,
                        Received = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21"),
                        Amount = Money.Parse("0.11") + Money.Parse("0.3") + Money.Parse("0.21"),
                    }
                }, result);
            }
        }

        [Fact]
        public void CanGetWalletBalanceSummary()
        {
            using(var tester = ServerTester.Create())
            {
                var alice1 = new Key();
                var alice2 = new Key();
                var alice3 = new Key();

                tester.ChainBuilder.EmitMoney(Money.Coins(1.5m), alice1);
                tester.ChainBuilder.EmitMoney(Money.Coins(1.0m), alice2);
                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                tester.Send<WalletModel>(HttpMethod.Post, "wallets", new WalletModel()
                {
                    Name = "Alice"
                });

                tester.Send<WalletAddress>(HttpMethod.Post, "wallets/Alice/addresses", new InsertWalletAddress()
                {
                    MergePast = true,
                    Address = alice1.GetBitcoinSecret(Network.TestNet).GetAddress()
                });

                var result = tester.SendGet<BalanceSummary>("wallets/Alice/summary?debug=true");
                Assert.True(result.Confirmed.Amount == Money.Coins(1.5m));

                result = tester.SendGet<BalanceSummary>("wallets/Alice/summary?debug=true&at=1");
                Assert.True(result.CacheHit == CacheHit.FullCache);

                tester.Send<WalletAddress>(HttpMethod.Post, "wallets/Alice/addresses", new InsertWalletAddress()
                {
                    MergePast = true,
                    Address = alice2.GetBitcoinSecret(Network.TestNet).GetAddress()
                });

                //Alice2 recieved money in the past, so cache should be invalidated
                result = tester.SendGet<BalanceSummary>("wallets/Alice/summary?debug=true");
                Assert.True(result.Confirmed.Amount == Money.Coins(2.5m));
                Assert.True(result.CacheHit == CacheHit.NoCache);

                result = tester.SendGet<BalanceSummary>("wallets/Alice/summary?debug=true&at=1");
                Assert.True(result.CacheHit == CacheHit.FullCache);

                tester.Send<WalletAddress>(HttpMethod.Post, "wallets/Alice/addresses", new InsertWalletAddress()
                {
                    MergePast = true,
                    Address = alice3.GetBitcoinSecret(Network.TestNet).GetAddress()
                });

                //Should not invalidate the cache, alice3 never received money
                result = tester.SendGet<BalanceSummary>("wallets/Alice/summary?debug=true&at=1");
                Assert.True(result.CacheHit == CacheHit.FullCache);
            }
        }

        [Fact]
        public void CanGetBalanceSummaryNoConcurrencyProblems()
        {
            using(var tester = ServerTester.Create())
            {
                var bob = new Key().GetBitcoinSecret(Network.TestNet);


                tester.ChainBuilder.SkipIndexer = true;
                var tx = tester.ChainBuilder.EmitMoney("1.0", bob);

                //Emit a block (no indexation)
                var block = tester.ChainBuilder.EmitBlock();
                //

                //Index the headers
                var indexer = tester.Configuration.Indexer.CreateIndexer();
                indexer.IndexChain(tester.ChainBuilder.Chain);
                tester.UpdateServerChain();
                //

                //No balances indexed, should be zero
                var result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = new BalanceSummaryDetails(),
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                }, result);

                //Index balances
                indexer.IndexOrderedBalance(tester.ChainBuilder.Chain.Height, block);
                var chk = indexer.GetCheckpoint(IndexerCheckpoints.Balances);
                chk.SaveProgress(tester.ChainBuilder.Chain.Tip);

                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");
                //Should now be indexed
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = new BalanceSummaryDetails(),
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        Amount = Money.Coins(1.0m),
                        Received = Money.Coins(1.0m),
                        TransactionCount = 1,
                    },
                    Confirmed = new BalanceSummaryDetails()
                    {
                        Amount = Money.Coins(1.0m),
                        Received = Money.Coins(1.0m),
                        TransactionCount = 1,
                    }
                }, result);
            }
        }
        [Fact]
        public void CanGetBalanceSummary()
        {
            using(var tester = ServerTester.Create())
            {
                var bob = new Key().GetBitcoinSecret(Network.TestNet);
                var alice = new Key().GetBitcoinSecret(Network.TestNet);
                AssertEx.HttpError(400, () => tester.SendGet<BalanceSummary>("balances/3FceQQyXMdiYoj5vLtu29VPgvDkxsEfxYH")); //Do not accept mainnet
                tester.SendGet<BalanceSummary>("balances/" + BitcoinAddress.Create("3FceQQyXMdiYoj5vLtu29VPgvDkxsEfxYH").ToNetwork(Network.TestNet));
                var result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails(),
                    UnConfirmed = new BalanceSummaryDetails(),
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                }, result);

                var tx = tester.ChainBuilder.EmitMoney("1.0", bob);

                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");

                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    UnConfirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("1.0"),
                        Amount = Money.Parse("1.0")
                    },
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("1.0"),
                        Amount = Money.Parse("1.0")
                    }
                }, result);

                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("1.0"),
                        Amount = Money.Parse("1.0")
                    },
                    UnConfirmed = new BalanceSummaryDetails(),
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("1.0"),
                        Amount = Money.Parse("1.0")
                    }
                }, result);
                //Should now take the cache
                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("1.0"),
                        Amount = Money.Parse("1.0")
                    },
                    UnConfirmed = new BalanceSummaryDetails(),
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("1.0"),
                        Amount = Money.Parse("1.0")
                    }
                }, result);

                var beforeAliceHeight = tester.ChainBuilder.Chain.Height;

                tx = tester.ChainBuilder.EmitMoney("1.5", bob);

                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("1.0"),
                        Amount = Money.Parse("1.0")
                    },
                    UnConfirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("1.5"),
                        Amount = Money.Parse("1.5")
                    },
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("2.5"),
                        Amount = Money.Parse("2.5")
                    },
                    Immature = new BalanceSummaryDetails()
                }, result);

                var forkPoint = tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("2.5"),
                        Amount = Money.Parse("2.5")
                    },
                    UnConfirmed = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("2.5"),
                        Amount = Money.Parse("2.5")
                    },
                    Immature = new BalanceSummaryDetails()
                }, result);

                tester.ChainBuilder.SendMoney(bob, alice, tx, Money.Parse("0.11"));

                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("2.5"),
                        Amount = Money.Parse("2.5")
                    },
                    UnConfirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0"),
                        Amount = -Money.Parse("0.11")
                    },
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 3,
                        Received = Money.Parse("2.5"),
                        Amount = Money.Parse("2.39")
                    }
                }, result);

                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 3,
                        Received = Money.Parse("2.5"),
                        Amount = Money.Parse("2.39")
                    },
                    UnConfirmed = new BalanceSummaryDetails(),
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 3,
                        Received = Money.Parse("2.5"),
                        Amount = Money.Parse("2.39")
                    }
                }, result);

                //Fork, the previous should be in pending now
                tester.ChainBuilder.SetTip(forkPoint.Header);
                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 2,
                        Received = Money.Parse("2.5"),
                        Amount = Money.Parse("2.5")
                    },
                    UnConfirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("0"),
                        Amount = -Money.Parse("0.11")
                    },
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 3,
                        Received = Money.Parse("2.5"),
                        Amount = Money.Parse("2.39")
                    }
                }, result);

                //Can ask old balance
                result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary?at=" + beforeAliceHeight);
                AssertEx.AssertJsonEqual(new BalanceSummary()
                {
                    Confirmed = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("1.0"),
                        Amount = Money.Parse("1.0")
                    },
                    UnConfirmed = null,
                    Immature = new BalanceSummaryDetails(),
                    Spendable = new BalanceSummaryDetails()
                    {
                        TransactionCount = 1,
                        Received = Money.Parse("1.0"),
                        Amount = Money.Parse("1.0")
                    }
                }, result);
            }
        }


        [Fact]
        public void BalanceDoesNotIncludeNonStandardCoin()
        {
            using(var tester = ServerTester.Create())
            {
                var bob = new Key().GetBitcoinSecret(Network.TestNet);
                tester.ChainBuilder.EmitMoney(Money.Coins(1.0m), bob);
                var prev = tester.ChainBuilder.EmitMoney(Money.Coins(1.5m), bob.ScriptPubKey + OpcodeType.OP_NOP);
                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                var tx = new Transaction()
                {
                    Inputs =
                    {
                        new TxIn()
                        {
                            PrevOut = prev.Outputs.AsCoins().First().Outpoint,
                            ScriptSig = bob.ScriptPubKey
                        }
                    }
                };
                tx.Sign(bob, false);

                tester.ChainBuilder.Broadcast(tx);
                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                var result = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary");
                Assert.Equal(1, result.Confirmed.TransactionCount);
            }
        }

        [Fact]
        public void CanManageWallet()
        {
            using(var tester = ServerTester.Create())
            {
                var alice1 = new Key().GetBitcoinSecret(Network.TestNet);
                var alice2 = new Key().GetBitcoinSecret(Network.TestNet);

                tester.ChainBuilder.EmitMoney(Money.Coins(1.0m), alice1);
                tester.ChainBuilder.EmitMoney(Money.Coins(1.5m), alice2);
                tester.ChainBuilder.EmitBlock();
                tester.Send<WalletModel>(HttpMethod.Post, "wallets", new WalletModel()
                {
                    Name = "Alice"
                });

                AssertEx.HttpError(409, () => tester.Send<WalletModel>(HttpMethod.Post, "wallets", new WalletModel()
                {
                    Name = "Alice"
                }));

                tester.Send<WalletAddress>(HttpMethod.Post, "wallets/Alice/addresses", new InsertWalletAddress()
                {
                    MergePast = false,
                    Address = alice1.GetAddress()
                });

                AssertEx.HttpError(409, () => tester.Send<WalletAddress>(HttpMethod.Post, "wallets/Alice/addresses", new InsertWalletAddress()
                {
                    MergePast = false,
                    Address = alice1.GetAddress()
                }));

                tester.Send<WalletAddress>(HttpMethod.Post, "wallets/Alice/addresses", new InsertWalletAddress()
                {
                    MergePast = false,
                    Address = alice2.GetAddress()
                });

                var balance = tester.SendGet<BalanceModel>("wallets/Alice/balance");
                Assert.True(balance.Operations.Count == 0);

                tester.ChainBuilder.EmitMoney(Money.Coins(0.1m), alice1);
                tester.ChainBuilder.EmitMoney(Money.Coins(1.52m), alice2);
                tester.ChainBuilder.EmitBlock();

                tester.UpdateServerChain();
                balance = tester.SendGet<BalanceModel>("wallets/Alice/balance");
                Assert.True(balance.Operations.Count == 2);

                var alice3 = new Key().GetBitcoinSecret(Network.TestNet);
                tester.ChainBuilder.EmitMoney(Money.Coins(1m), alice3);
                tester.ChainBuilder.EmitBlock();

                tester.Send<WalletAddress>(HttpMethod.Post, "wallets/Alice/addresses", new InsertWalletAddress()
                {
                    MergePast = true,
                    Address = alice3.GetAddress(),
                    UserData = new JValue("hello")
                });

                balance = tester.SendGet<BalanceModel>("wallets/Alice/balance");
                Assert.True(balance.Operations.Count == 3);

                var summary = tester.SendGet<BalanceSummary>("wallets/Alice/summary");
                Assert.True(summary.Spendable.TransactionCount == 3);
            }
        }


        [Fact]
        public void CanManageKeyGenerationErrorCheck()
        {
            using(var tester = ServerTester.Create())
            {
                var alice = new ExtKey().GetWif(Network.TestNet);
                var pubkeyAlice = alice.ExtKey.Neuter().GetWif(Network.TestNet);

                tester.Send<HDKeySet>(HttpMethod.Post, "wallets/alice/keysets", new HDKeySet()
                {
                    Name = "a",
                    ExtPubKeys = new BitcoinExtPubKey[] { pubkeyAlice }
                });
                AssertEx.HttpError(409, () => tester.Send<HDKeySet>(HttpMethod.Post, "wallets/alice/keysets", new HDKeySet()
                {
                    Name = "a",
                    ExtPubKeys = new BitcoinExtPubKey[] { pubkeyAlice }
                }));

                AssertEx.HttpError(400, () => tester.Send<HDKeySet>(HttpMethod.Post, "wallets/alice/keysets", new HDKeySet()
                {
                    Name = "b",
                    ExtPubKeys = new BitcoinExtPubKey[] { pubkeyAlice },
                    SignatureCount = 2
                }));

                AssertEx.HttpError(400, () => tester.Send<HDKeySet>(HttpMethod.Post, "wallets/alice/keysets", new HDKeySet()
                {
                    Name = "b"
                }));

                AssertEx.HttpError(400, () => tester.Send<HDKeySet>(HttpMethod.Post, "wallets/alice/keysets", new HDKeySet()
                {
                    Name = "c",
                    ExtPubKeys = new BitcoinExtPubKey[] { pubkeyAlice },
                    Path = KeyPath.Parse("1/2/3'"),
                    SignatureCount = 1
                }));
            }
        }

        [Fact]
        public void HDKeysetsAreCorrectlyTracked()
        {
            using(var tester = ServerTester.Create())
            {
                var alice = new ExtKey().GetWif(Network.TestNet);
                var pubkeyAlice = alice.ExtKey.Neuter().GetWif(Network.TestNet);
                var aliceName = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10);

                var listener = tester.CreateListenerTester();
                tester.ChainBuilder.SkipIndexer = true;

                var alice1 = pubkeyAlice.ExtPubKey.Derive(1).PubKey.GetAddress(Network.TestNet);
                tester.ChainBuilder.EmitMoney(Money.Coins(1.0m), alice1);
                var alice20 = pubkeyAlice.ExtPubKey.Derive(20).PubKey.GetAddress(Network.TestNet);
                tester.ChainBuilder.EmitMoney(Money.Coins(1.1m), alice20); //Should be found because alice1 "extend" the scan
                var alice41 = pubkeyAlice.ExtPubKey.Derive(41).PubKey.GetAddress(Network.TestNet);
                var waiter = listener.WaitMessageAsync(tester.Configuration.Topics.NewTransactions);
                var tx = tester.ChainBuilder.EmitMoney(Money.Coins(1.2m), alice41); //But 41 is "outside" the gap limit
                Assert.True(waiter.Wait(10000));

                var balance = tester.SendGet<BalanceModel>("balances/" + alice1);
                Assert.Equal(1, balance.Operations.Count);

                tester.Send<WalletModel>(HttpMethod.Post, "wallets", new WalletModel()
                {
                    Name = aliceName
                });
                tester.Send<HDKeySet>(HttpMethod.Post, "wallets/" + aliceName + "/keysets", new HDKeySet()
                {
                    Name = "SingleNoP2SH",
                    ExtPubKeys = new BitcoinExtPubKey[] { pubkeyAlice },
                    P2SH = false
                });

                balance = tester.SendGet<BalanceModel>("wallets/" + aliceName + "/balance");
                Assert.Equal(2, balance.Operations.Count);
                Assert.True(balance.Operations.Any(op => op.Amount == Money.Coins(1.0m)));
                Assert.True(balance.Operations.Any(op => op.Amount == Money.Coins(1.1m)));

                var keyset = tester.Send<KeySetData>(HttpMethod.Get, "wallets/" + aliceName + "/keysets/SingleNoP2SH");
                Assert.Equal(21, keyset.State.NextUnused);

                var alice21 = pubkeyAlice.ExtPubKey.Derive(21).PubKey.GetAddress(Network.TestNet);
                tester.ChainBuilder.EmitMoney(Money.Coins(1.3m), alice21); //Receiving on alice21 should fire a scan of 41

                var waiter2 = listener.WaitMessageAsync(tester.Configuration.Topics.NewBlocks);
                tester.ChainBuilder.EmitBlock();
                waiter2.Wait();
                tester.UpdateServerChain();

                balance = tester.SendGet<BalanceModel>("wallets/" + aliceName + "/balance");
                Assert.Equal(4, balance.Operations.Count);
                Assert.True(balance.Operations.All(o => o.Confirmations == 1));

                keyset = tester.Send<KeySetData>(HttpMethod.Get, "wallets/" + aliceName + "/keysets/SingleNoP2SH");
                Assert.Equal(42, keyset.State.NextUnused);
            }
        }

        [Fact]
        public void CanManageKeyGeneration()
        {
            using(var tester = ServerTester.Create())
            {
                var alice = new ExtKey().GetWif(Network.TestNet);
                var pubkeyAlice = alice.ExtKey.Neuter().GetWif(Network.TestNet);

                tester.Send<WalletModel>(HttpMethod.Post, "wallets", new WalletModel()
                {
                    Name = "alice"
                });
                tester.Send<HDKeySet>(HttpMethod.Post, "wallets/alice/keysets", new HDKeySet()
                {
                    Name = "SingleNoP2SH",
                    Path = KeyPath.Parse("1/2/3"),
                    ExtPubKeys = new BitcoinExtPubKey[] { pubkeyAlice },
                    P2SH = false
                });
                var result = tester.Send<HDKeyData>(HttpMethod.Get, "wallets/alice/keysets/SingleNoP2SH/unused/0");
                Assert.Equal(pubkeyAlice.ExtPubKey.Derive(KeyPath.Parse("1/2/3/0")).PubKey.GetAddress(Network.TestNet), result.Address);
                result = tester.Send<HDKeyData>(HttpMethod.Get, "wallets/alice/keysets/SingleNoP2SH/unused/1");
                Assert.Equal(result.Address, pubkeyAlice.ExtPubKey.Derive(KeyPath.Parse("1/2/3/1")).PubKey.GetAddress(Network.TestNet));
                Assert.Equal(result.Path, KeyPath.Parse("1/2/3/1"));
                Assert.Null(result.RedeemScript);

                tester.Send<HDKeySet>(HttpMethod.Post, "wallets/alice/keysets", new HDKeySet()
                {
                    Name = "Single",
                    Path = KeyPath.Parse("1/2/3"),
                    ExtPubKeys = new BitcoinExtPubKey[] { pubkeyAlice },
                });
                result = tester.Send<HDKeyData>(HttpMethod.Get, "wallets/alice/keysets/Single/unused/0");
                var redeem = pubkeyAlice.ExtPubKey.Derive(KeyPath.Parse("1/2/3/0")).PubKey.ScriptPubKey;
                Assert.Equal(result.Address, redeem.Hash.GetAddress(Network.TestNet));
                Assert.Equal(result.RedeemScript, redeem);
                Assert.Equal(result.ScriptPubKey, redeem.Hash.ScriptPubKey);

                var bob = new ExtKey().GetWif(Network.TestNet);
                var pubkeyBob = bob.ExtKey.Neuter().GetWif(Network.TestNet);

                //Can generate key on multi sig
                tester.Send<HDKeySet>(HttpMethod.Post, "wallets/alice/keysets", new HDKeySet()
                {
                    Name = "Multi",
                    Path = KeyPath.Parse("1/2/3"),
                    ExtPubKeys = new BitcoinExtPubKey[] { pubkeyAlice, pubkeyBob },
                    SignatureCount = 1,
                    P2SH = true,
                    LexicographicOrder = false
                });
                result = tester.Send<HDKeyData>(HttpMethod.Get, "wallets/alice/keysets/Multi/unused/0");
                redeem = PayToMultiSigTemplate
                            .Instance
                            .GenerateScriptPubKey(1,
                            pubkeyAlice.ExtPubKey.Derive(KeyPath.Parse("1/2/3/0")).PubKey,
                            pubkeyBob.ExtPubKey.Derive(KeyPath.Parse("1/2/3/0")).PubKey);
                Assert.Equal(result.Address, redeem.Hash.GetAddress(Network.TestNet));
                Assert.Equal(result.RedeemScript, redeem);
                Assert.Equal(result.ScriptPubKey, redeem.Hash.ScriptPubKey);

                var sets = tester.SendGet<KeySetData[]>("wallets/alice/keysets");
                Assert.Equal(3, sets.Length);

                var keys = tester.SendGet<HDKeyData[]>("wallets/alice/keysets/Multi/keys");
                Assert.Equal(20, keys.Length);

                Assert.True(tester.Send<bool>(HttpMethod.Delete, "wallets/alice/keysets/Multi"));
                AssertEx.HttpError(404, () => tester.Send<bool>(HttpMethod.Delete, "wallets/alice/keysets/Multi"));
                AssertEx.HttpError(404, () => tester.SendGet<HDKeyData[]>("wallets/alice/keysets/Multi/keys"));
                AssertEx.HttpError(404, () => tester.Send<HDKeyData>(HttpMethod.Get, "wallets/alice/keysets/Multi/unused/0"));
            }
        }

        static Money Dust = new Money(546);
        static StandardTransactionPolicy Policy = new StandardTransactionPolicy()
        {
            MinRelayTxFee = new FeeRate(Money.Satoshis(1000))
        };
        [Fact]
        public void CanGetColoredBalance()
        {
            using(var tester = ServerTester.Create())
            {
                var goldGuy = new Key();
                var silverGuy = new Key();
                var gold = new AssetId(goldGuy);
                var silver = new AssetId(silverGuy);

                var bob = new Key().GetBitcoinSecret(Network.TestNet);
                var alice = new Key().GetBitcoinSecret(Network.TestNet);

                tester.AssertTotal(bob.GetAddress(), new Money(0));
                tester.AssertTotal(bob.GetAddress(), new AssetMoney(gold, 0));
                tester.AssertTotal(bob.GetAddress(), new AssetMoney(silver, 0));

                var tx = tester.ChainBuilder.EmitMoney(Money.Coins(100), goldGuy);
                var goldIssuance = new IssuanceCoin(tx.Outputs.AsCoins().First());
                tx = tester.ChainBuilder.EmitMoney(Money.Coins(95), silverGuy);
                var silverIssuance = new IssuanceCoin(tx.Outputs.AsCoins().First());
                tester.ChainBuilder.EmitMoney(Money.Coins(90), bob);
                tester.ChainBuilder.EmitMoney(Money.Coins(50), alice);

                tester.AssertTotal(bob.GetAddress(), Money.Coins(90));
                tester.AssertTotal(bob.GetAddress(), new AssetMoney(gold, 0));
                tester.AssertTotal(bob.GetAddress(), new AssetMoney(silver, 0));

                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                //Send gold to bob
                tx = new TransactionBuilder()
                {
                    StandardTransactionPolicy = Policy
                }
                     .AddKeys(goldGuy)
                     .AddCoins(goldIssuance)
                     .IssueAsset(bob, new AssetMoney(gold, 1000))
                     .SetChange(goldGuy)
                     .Then()
                     .AddKeys(bob)
                     .AddCoins(tester.GetUnspentCoins(bob))
                     .Send(goldGuy, Money.Coins(5.0m))
                     .SetChange(bob)
                     .BuildTransaction(true);
                tester.ChainBuilder.Broadcast(tx);

                tester.AssertTotal(bob, Money.Coins(85));
                tester.AssertTotal(bob, new AssetMoney(gold, 1000));
                tester.AssertTotal(bob, new AssetMoney(silver, 0));

                //Send silver to Alice
                tx = new TransactionBuilder()
                {
                    StandardTransactionPolicy = Policy
                }
                     .AddKeys(silverGuy)
                     .AddCoins(silverIssuance)
                     .IssueAsset(alice, new AssetMoney(silver, 100))
                     .SetChange(silverGuy)
                     .Then()
                     .AddKeys(alice)
                     .AddCoins(tester.GetUnspentCoins(alice))
                     .Send(silverGuy, Money.Coins(2.5m))
                     .SetChange(alice)
                     .BuildTransaction(true);
                tester.ChainBuilder.Broadcast(tx);

                tester.AssertTotal(alice, Money.Coins(47.5m));
                tester.AssertTotal(alice, new AssetMoney(gold, 0));
                tester.AssertTotal(alice, new AssetMoney(silver, 100));

                tester.ChainBuilder.EmitBlock();

                //Bob and alice swap
                tx = new TransactionBuilder()
                {
                    StandardTransactionPolicy = Policy
                }
                     .AddKeys(alice)
                     .AddCoins(tester.GetUnspentCoins(alice))
                     .SendAsset(bob, new AssetMoney(silver, 9))
                     .Send(bob, Money.Coins(1.0m))
                     .SetChange(alice)
                     .Then()
                     .AddKeys(bob)
                     .AddCoins(tester.GetUnspentCoins(bob))
                     .SendAsset(alice, new AssetMoney(gold, 10))
                     .Send(alice, Money.Coins(1.5m))
                     .SetChange(bob)
                     .BuildTransaction(true);
                tester.ChainBuilder.Broadcast(tx);

                tester.AssertTotal(alice, Money.Coins(48m) - Dust);
                tester.AssertTotal(alice, new AssetMoney(gold, 10));
                tester.AssertTotal(alice, new AssetMoney(silver, 91));

                tester.AssertTotal(bob, Money.Coins(84.5m) - Dust);
                tester.AssertTotal(bob, new AssetMoney(gold, 990));
                tester.AssertTotal(bob, new AssetMoney(silver, 9));

                tester.ChainBuilder.EmitBlock();
                tester.UpdateServerChain();

                for(int i = 0; i < 2; i++)
                {
                    tester.AssertTotal(bob, Money.Coins(84.5m) - Dust);
                    tester.AssertTotal(bob, new AssetMoney(gold, 990));
                    tester.AssertTotal(bob, new AssetMoney(silver, 9));
                }
                var summary = tester.SendGet<BalanceSummary>("balances/" + bob.GetAddress() + "/summary?colored=true");
                Assert.NotNull(summary.Confirmed.Assets);
            }
        }

        [Fact]
        public void CanGetBalance()
        {
            using(var tester = ServerTester.Create())
            {
                var bob = new Key().GetBitcoinSecret(Network.TestNet);
                var balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress());
                tester.AssertTotal(bob.GetAddress(), 0);
                Assert.True(balance.Operations.Count == 0);

                var tx = tester.ChainBuilder.EmitMoney(Money.Coins(1.00m), bob);
                balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress());
                tester.AssertTotal(bob.GetAddress(), Money.Coins(1.0m));
                Assert.True(balance.Operations.Count == 1);
                Assert.True(balance.Operations[0].Confirmations == 0);
                Assert.True(balance.Operations[0].BlockId == null);

                var b = tester.ChainBuilder.EmitBlock(); //1
                tester.UpdateServerChain();
                balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress());
                Assert.True(balance.Operations[0].Confirmations == 1);
                Assert.True(balance.Operations[0].BlockId == b.GetHash());

                tx = new TransactionBuilder()
                      .AddKeys(bob)
                      .AddCoins(new Coin(tx, 0U))
                      .SendFees(Money.Coins(0.05m))
                      .SetChange(bob)
                      .BuildTransaction(true);
                tester.ChainBuilder.Broadcast(tx);

                balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress());
                Assert.True(balance.Operations.Count == 2);
                Assert.True(balance.Operations[0].Confirmations == 0);
                Assert.True(balance.Operations[0].TransactionId == tx.GetHash());

                balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress() + "?unspentOnly=true");
                Assert.True(balance.Operations.Count == 1);
                Assert.True(balance.Operations[0].Confirmations == 0);
                Assert.True(balance.Operations[0].TransactionId == tx.GetHash());
                tester.AssertTotal(bob.GetAddress(), Money.Coins(0.95m));

                tester.ChainBuilder.EmitBlock(); //2
                tester.UpdateServerChain();

                balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress());
                Assert.True(balance.Operations.Count == 2);
                Assert.True(balance.Operations[0].Confirmations == 1);
                tester.AssertTotal(bob.GetAddress(), Money.Coins(0.95m));

                tester.ChainBuilder.EmitMoney(Money.Parse("0.02"), bob, coinbase: true);
                tester.ChainBuilder.EmitBlock(); //3
                tester.UpdateServerChain();

                balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress());
                Assert.True(balance.Operations.Count == 2); //Immature should not appear
                tester.AssertTotal(bob.GetAddress(), Money.Coins(0.95m));

                balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress() + "?includeimmature=true");
                Assert.True(balance.Operations.Count == 3); //Except if asked for

                balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress() + "?from=1");
                Assert.True(balance.Operations.Count == 1); //Should only have the operation at 1
                balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress() + "?from=1&includeimmature=true");
                Assert.True(balance.Operations.Count == 1); //Should only have the operation at 1

                balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress() + "?until=2&includeimmature=true");
                Assert.True(balance.Operations.Count == 2); //Should only have the operation at 2 and the immature

                balance = tester.SendGet<BalanceModel>("balances/" + bob.GetAddress() + "?from=2&until=1&includeimmature=true");
                Assert.True(balance.Operations.Count == 2); //Should only have the 2 operations

                AssertEx.HttpError(400, () => tester.SendGet<GetTransactionResponse>("balances/000lol"));    // todo: there's a risk that Dispose() will be called for tester when the lambda executes
            }
        }

        [Fact]
        public void CanWhatIsIt()
        {
            //TODO: These tests are fragile, a simple api change can break them. We should try to be more relax.
            using(var tester = ServerTester.Create())
            {
                tester.ChainBuilder.UploadBlock = true;
                var bob = new BitcoinSecret("KxMVn7SRNkWTfVa78UXCmsc6Kyp3aQZydnyzGzNBrRg2T9X1u4er");
                //whatisit/[address|txId|blockId|blockheader|base58|transaction|script|scriptbytes]

                //Can parse base58 secret (depends on MainNet)
                AssertWhatIsIt(
                    tester,
                    bob.ToString(),
					"{  \"publicKey\": {    \"hex\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b\",    \"isCompressed\": true,    \"address\": {      \"isP2SH\": false,      \"hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",      \"coloredAddress\": \"akJE6ZGzCRGueyTwZdD8beZZ7rvH2oRDzGP\",      \"scriptPubKey\": {        \"hash160\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",        \"hash256\": \"2832bbecde384ac895b63744b9c09a7b6be647352b38fdac544b6da01b8abfae\",        \"address\": \"18GDK7Arwo1y7DnCab2QzheYXK6rqW8zzM\",        \"raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",        \"asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"      },      \"redeemScript\": null,      \"publicKey\": null,      \"base58\": \"18GDK7Arwo1y7DnCab2QzheYXK6rqW8zzM\",      \"type\": \"PUBKEY_ADDRESS\",      \"network\": \"MainNet\"    },    \"p2shAddress\": {      \"isP2SH\": true,      \"hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"coloredAddress\": \"anYvNDD3eJVQ5G17C4d99fB5hzVzP3hARES\",      \"scriptPubKey\": {        \"hash160\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",        \"hash256\": \"e08a70b09ed1604f91e85208594531a53099cf1b63e3c7bcc9386399dedda970\",        \"address\": \"3NxUy3EJq1WPPkwq212y1KB8etpD4aTgCh\",        \"raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",        \"asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"      },      \"redeemScript\": {        \"hash160\": \"e947748c6687299740a448d524dc7aef830023a7\",        \"hash256\": \"dd4fd0f0036155064b97c45fd07d3b3a525672d4fb5f7c198c6afc84c571943e\",        \"address\": null,        \"raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",        \"asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"      },      \"publicKey\": null,      \"base58\": \"3NxUy3EJq1WPPkwq212y1KB8etpD4aTgCh\",      \"type\": \"SCRIPT_ADDRESS\",      \"network\": \"MainNet\"    },    \"scriptPubKey\": {      \"hash160\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"hash256\": \"dd4fd0f0036155064b97c45fd07d3b3a525672d4fb5f7c198c6afc84c571943e\",      \"address\": null,      \"raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",      \"asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"    }  },  \"base58\": \"KxMVn7SRNkWTfVa78UXCmsc6Kyp3aQZydnyzGzNBrRg2T9X1u4er\",  \"type\": \"SECRET_KEY\",  \"network\": \"MainNet\"}"
                    );
                //Can parse address (depends on TestNet)
                bob = bob.PrivateKey.GetBitcoinSecret(Network.TestNet);
                AssertWhatIsIt(
                    tester,
                    bob.GetAddress().ToString(),
					"{  \"isP2SH\": false,  \"hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",  \"coloredAddress\": \"bWxk3rL5BEJLukaRBLn6yUUmSiusjigXNuU\",  \"scriptPubKey\": {    \"hash160\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",    \"hash256\": \"2832bbecde384ac895b63744b9c09a7b6be647352b38fdac544b6da01b8abfae\",    \"address\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",    \"raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",    \"asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"  },  \"redeemScript\": null,  \"publicKey\": null,  \"base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",  \"type\": \"PUBKEY_ADDRESS\",  \"network\": \"TestNet\"}"
                    );

                //Can find transaction
                var tx = tester.ChainBuilder.EmitMoney("1.0", bob, false);
                AssertWhatIsIt(
                    tester,
                    tx.GetHash().ToString(),
                    null
                    );
                tester.ChainBuilder.Broadcast(tx);
                AssertWhatIsIt(
                    tester,
                    tx.GetHash().ToString(),
                    "{  \"transaction\": \"01000008000100e1f505000000001976a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac00000000\",  \"transactionId\": \"f2454c22812b37c9a8b7fc04cc9efe587fe3e559a922a0fd880af15a39161e33\",  \"isCoinbase\": false,  \"block\": null,  \"spentCoins\": [],  \"receivedCoins\": [    {      \"transactionId\": \"f2454c22812b37c9a8b7fc04cc9efe587fe3e559a922a0fd880af15a39161e33\",      \"index\": 0,      \"value\": 100000000,      \"scriptPubKey\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",      \"redeemScript\": null    }  ], \"firstSeen\": 0, \"fees\": -100000000}"
                    );
                /////

                //Can find block by id
                AssertWhatIsIt(
                    tester,
                    "fc6f54710fefb6c27cad94c6103ceec7b206a80b34b1e4458355d88d257337d5",
                    null
                    );
                var b = tester.ChainBuilder.EmitBlock(1); //fc6f54710fefb6c27cad94c6103ceec7b206a80b34b1e4458355d88d257337d5
                tester.UpdateServerChain();
                AssertWhatIsIt(
                    tester,
                    "fc6f54710fefb6c27cad94c6103ceec7b206a80b34b1e4458355d88d257337d5",
					"{  \"additionalInformation\": {    \"blockId\": \"fc6f54710fefb6c27cad94c6103ceec7b206a80b34b1e4458355d88d257337d5\",    \"blockHeader\": \"0200000043497fd7f826957108f4a30fd9cec3aeba79972084e90ead01ea3309000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001000000\",    \"height\": 1,    \"confirmations\": 1,    \"medianTimePast\": \"2011-02-03T08:16:42+09:00\",    \"blockTime\": \"1970-01-01T09:00:00+09:00\"  },  \"block\": null}"
                    );
                /////

                //Can find block by height
                AssertWhatIsIt(
                    tester,
                    "1",
					"{  \"additionalInformation\": {    \"blockId\": \"fc6f54710fefb6c27cad94c6103ceec7b206a80b34b1e4458355d88d257337d5\",    \"blockHeader\": \"0200000043497fd7f826957108f4a30fd9cec3aeba79972084e90ead01ea3309000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001000000\",    \"height\": 1,    \"confirmations\": 1,    \"medianTimePast\": \"2011-02-03T08:16:42+09:00\",    \"blockTime\": \"1970-01-01T09:00:00+09:00\"  },  \"block\": null}"
                    );
                AssertWhatIsIt(
                   tester,
                   "2",
                   null
                   );
                /////

                //Can find public key
                AssertWhatIsIt(
                    tester,
                    bob.PrivateKey.PubKey.ToHex(),
					"{  \"hex\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b\",  \"isCompressed\": true,  \"address\": {    \"isP2SH\": false,    \"hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",    \"coloredAddress\": \"bWxk3rL5BEJLukaRBLn6yUUmSiusjigXNuU\",    \"scriptPubKey\": {      \"hash160\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",      \"hash256\": \"2832bbecde384ac895b63744b9c09a7b6be647352b38fdac544b6da01b8abfae\",      \"address\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",      \"raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",      \"asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"    },    \"redeemScript\": null,    \"publicKey\": null,    \"base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",    \"type\": \"PUBKEY_ADDRESS\",    \"network\": \"TestNet\"  },  \"p2shAddress\": {    \"isP2SH\": true,    \"hash\": \"e947748c6687299740a448d524dc7aef830023a7\",    \"coloredAddress\": \"c7QUaGwyfuwuRTnjjjkm2H84yCrCYrQFUQZ\",    \"scriptPubKey\": {      \"hash160\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",      \"hash256\": \"e08a70b09ed1604f91e85208594531a53099cf1b63e3c7bcc9386399dedda970\",      \"address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",      \"raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",      \"asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"    },    \"redeemScript\": {      \"hash160\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"hash256\": \"dd4fd0f0036155064b97c45fd07d3b3a525672d4fb5f7c198c6afc84c571943e\",      \"address\": null,      \"raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",      \"asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"    },    \"publicKey\": null,    \"base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",    \"type\": \"SCRIPT_ADDRESS\",    \"network\": \"TestNet\"  },  \"scriptPubKey\": {    \"hash160\": \"e947748c6687299740a448d524dc7aef830023a7\",    \"hash256\": \"dd4fd0f0036155064b97c45fd07d3b3a525672d4fb5f7c198c6afc84c571943e\",    \"address\": null,    \"raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",    \"asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"  }}"
                    );
                ////

                //Can find public key from address if divulged
                tx = new TransactionBuilder()
                        .AddKeys(bob)
                        .AddCoins(new Coin(tx, 0U))
                        .SendFees(Money.Coins(0.05m))
                        .SetChange(bob)
                        .BuildTransaction(true);
                tester.ChainBuilder.Broadcast(tx);
                AssertWhatIsIt(
                    tester,
                    bob.GetAddress().ToString(),
					"{  \"isP2SH\": false,  \"hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",  \"coloredAddress\": \"bWxk3rL5BEJLukaRBLn6yUUmSiusjigXNuU\",  \"scriptPubKey\": {    \"hash160\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",    \"hash256\": \"2832bbecde384ac895b63744b9c09a7b6be647352b38fdac544b6da01b8abfae\",    \"address\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",    \"raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",    \"asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"  },  \"redeemScript\": null,  \"publicKey\": {    \"hex\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b\",    \"isCompressed\": true,    \"address\": {      \"isP2SH\": false,      \"hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",      \"coloredAddress\": \"bWxk3rL5BEJLukaRBLn6yUUmSiusjigXNuU\",      \"scriptPubKey\": {        \"hash160\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",        \"hash256\": \"2832bbecde384ac895b63744b9c09a7b6be647352b38fdac544b6da01b8abfae\",        \"address\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",        \"raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",        \"asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"      },      \"redeemScript\": null,      \"publicKey\": null,      \"base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",      \"type\": \"PUBKEY_ADDRESS\",      \"network\": \"TestNet\"    },    \"p2shAddress\": {      \"isP2SH\": true,      \"hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"coloredAddress\": \"c7QUaGwyfuwuRTnjjjkm2H84yCrCYrQFUQZ\",      \"scriptPubKey\": {        \"hash160\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",        \"hash256\": \"e08a70b09ed1604f91e85208594531a53099cf1b63e3c7bcc9386399dedda970\",        \"address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",        \"raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",        \"asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"      },      \"redeemScript\": {        \"hash160\": \"e947748c6687299740a448d524dc7aef830023a7\",        \"hash256\": \"dd4fd0f0036155064b97c45fd07d3b3a525672d4fb5f7c198c6afc84c571943e\",        \"address\": null,        \"raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",        \"asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"      },      \"publicKey\": null,      \"base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",      \"type\": \"SCRIPT_ADDRESS\",      \"network\": \"TestNet\"    },    \"scriptPubKey\": {      \"hash160\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"hash256\": \"dd4fd0f0036155064b97c45fd07d3b3a525672d4fb5f7c198c6afc84c571943e\",      \"address\": null,      \"raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",      \"asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"    }  },  \"base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",  \"type\": \"PUBKEY_ADDRESS\",  \"network\": \"TestNet\"}"
                    );
                //Should also find with the pub key hash
                AssertWhatIsIt(
                    tester,
                    "4fa965c94a53aaa0d87d1d05a826d77906ff5219",
					"{  \"isP2SH\": false,  \"hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",  \"coloredAddress\": \"bWxk3rL5BEJLukaRBLn6yUUmSiusjigXNuU\",  \"scriptPubKey\": {    \"hash160\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",    \"hash256\": \"2832bbecde384ac895b63744b9c09a7b6be647352b38fdac544b6da01b8abfae\",    \"address\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",    \"raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",    \"asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"  },  \"redeemScript\": null,  \"publicKey\": {    \"hex\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b\",    \"isCompressed\": true,    \"address\": {      \"isP2SH\": false,      \"hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",      \"coloredAddress\": \"bWxk3rL5BEJLukaRBLn6yUUmSiusjigXNuU\",      \"scriptPubKey\": {        \"hash160\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",        \"hash256\": \"2832bbecde384ac895b63744b9c09a7b6be647352b38fdac544b6da01b8abfae\",        \"address\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",        \"raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",        \"asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"      },      \"redeemScript\": null,      \"publicKey\": null,      \"base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",      \"type\": \"PUBKEY_ADDRESS\",      \"network\": \"TestNet\"    },    \"p2shAddress\": {      \"isP2SH\": true,      \"hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"coloredAddress\": \"c7QUaGwyfuwuRTnjjjkm2H84yCrCYrQFUQZ\",      \"scriptPubKey\": {        \"hash160\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",        \"hash256\": \"e08a70b09ed1604f91e85208594531a53099cf1b63e3c7bcc9386399dedda970\",        \"address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",        \"raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",        \"asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"      },      \"redeemScript\": {        \"hash160\": \"e947748c6687299740a448d524dc7aef830023a7\",        \"hash256\": \"dd4fd0f0036155064b97c45fd07d3b3a525672d4fb5f7c198c6afc84c571943e\",        \"address\": null,        \"raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",        \"asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"      },      \"publicKey\": null,      \"base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",      \"type\": \"SCRIPT_ADDRESS\",      \"network\": \"TestNet\"    },    \"scriptPubKey\": {      \"hash160\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"hash256\": \"dd4fd0f0036155064b97c45fd07d3b3a525672d4fb5f7c198c6afc84c571943e\",      \"address\": null,      \"raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",      \"asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"    }  },  \"base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",  \"type\": \"PUBKEY_ADDRESS\",  \"network\": \"TestNet\"}"
                    );
                /////

                //Can find redeem script if divulged
                tx = tester.ChainBuilder.EmitMoney(Money.Coins(1.0m), bob.PrivateKey.PubKey.ScriptPubKey.Hash);
                tx = new TransactionBuilder()
                        .AddKeys(bob)
                        .AddCoins(new Coin(tx, 0U))
                        .AddKnownRedeems(bob.PrivateKey.PubKey.ScriptPubKey)
                        .SendFees(Money.Coins(0.05m))
                        .SetChange(bob)
                        .BuildTransaction(true);
                tester.ChainBuilder.Broadcast(tx);
                AssertWhatIsIt(
                   tester,
                   bob.PrivateKey.PubKey.ScriptPubKey.GetScriptAddress(Network.TestNet).ToString(),
				   "{  \"isP2SH\": true,  \"hash\": \"e947748c6687299740a448d524dc7aef830023a7\",  \"coloredAddress\": \"c7QUaGwyfuwuRTnjjjkm2H84yCrCYrQFUQZ\",  \"scriptPubKey\": {    \"hash160\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",    \"hash256\": \"e08a70b09ed1604f91e85208594531a53099cf1b63e3c7bcc9386399dedda970\",    \"address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",    \"raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",    \"asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"  },  \"redeemScript\": {    \"hash160\": \"e947748c6687299740a448d524dc7aef830023a7\",    \"hash256\": \"dd4fd0f0036155064b97c45fd07d3b3a525672d4fb5f7c198c6afc84c571943e\",    \"address\": null,    \"raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",    \"asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"  },  \"publicKey\": null,  \"base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",  \"type\": \"SCRIPT_ADDRESS\",  \"network\": \"TestNet\"}"
                   );
                //Should also find with the script hash
                AssertWhatIsIt(
                   tester,
                   "e947748c6687299740a448d524dc7aef830023a7",
				   "{  \"isP2SH\": true,  \"hash\": \"e947748c6687299740a448d524dc7aef830023a7\",  \"coloredAddress\": \"c7QUaGwyfuwuRTnjjjkm2H84yCrCYrQFUQZ\",  \"scriptPubKey\": {    \"hash160\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",    \"hash256\": \"e08a70b09ed1604f91e85208594531a53099cf1b63e3c7bcc9386399dedda970\",    \"address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",    \"raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",    \"asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"  },  \"redeemScript\": {    \"hash160\": \"e947748c6687299740a448d524dc7aef830023a7\",    \"hash256\": \"dd4fd0f0036155064b97c45fd07d3b3a525672d4fb5f7c198c6afc84c571943e\",    \"address\": null,    \"raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",    \"asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"  },  \"publicKey\": null,  \"base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",  \"type\": \"SCRIPT_ADDRESS\",  \"network\": \"TestNet\"}"
                   );
                ////

                //Can decode script
                AssertWhatIsIt(
                  tester,
                  "21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac",
				  "{  \"hash160\": \"e947748c6687299740a448d524dc7aef830023a7\",  \"hash256\": \"dd4fd0f0036155064b97c45fd07d3b3a525672d4fb5f7c198c6afc84c571943e\",  \"address\": null,  \"raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",  \"asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"}"
                  );
                AssertWhatIsIt(
                  tester,
                  "025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG",
				  "{  \"hash160\": \"e947748c6687299740a448d524dc7aef830023a7\",  \"hash256\": \"dd4fd0f0036155064b97c45fd07d3b3a525672d4fb5f7c198c6afc84c571943e\",  \"address\": null,  \"raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",  \"asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"}"
                  );
				AssertWhatIsIt(tester, "76a9142e9ced3c2b5a04cf46b2c9fed28cd80672e61ab688ac", "{  \"hash160\": \"65ecbd17539541b2e28862b6367c3486014e8cdb\",  \"hash256\": \"25ae59f9a2a14c2c0c0f9ad0e72b17e28df39b7bb72532df645812d190740c39\",  \"address\": \"mjmRNLU3cYrijMm6Ndf4bftdymRrHLPvrw\",  \"raw\": \"76a9142e9ced3c2b5a04cf46b2c9fed28cd80672e61ab688ac\",  \"asm\": \"OP_DUP OP_HASH160 2e9ced3c2b5a04cf46b2c9fed28cd80672e61ab6 OP_EQUALVERIFY OP_CHECKSIG\"}");
                ////

                //Can decode signature
                AssertWhatIsIt(
                    tester,
                    "3045022100a8a45e762fbda89f16a08de25274257eb2b7d9fbf481d359b28e47205c8bdc2f022007917ee618ae55a8936c75ad603623671f27ce8591010b769718ebc5ff295cf001",
                    "{  \"Raw\": \"3045022100a8a45e762fbda89f16a08de25274257eb2b7d9fbf481d359b28e47205c8bdc2f022007917ee618ae55a8936c75ad603623671f27ce8591010b769718ebc5ff295cf001\",  \"R\": \"a8a45e762fbda89f16a08de25274257eb2b7d9fbf481d359b28e47205c8bdc2f\",  \"S\": \"7917ee618ae55a8936c75ad603623671f27ce8591010b769718ebc5ff295cf0\",  \"AnyoneCanPay\": false,  \"SigHash\": \"All\"}"
                    );
                /////

                //Can decode block header
                AssertWhatIsIt(
                    tester,
                    Encoders.Hex.EncodeData(Network.Main.GetGenesis().Header.ToBytes()),
                    "{  \"Version\": \"1\",  \"Hash\": \"000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f\",  \"Previous\": \"0000000000000000000000000000000000000000000000000000000000000000\",  \"Time\": \"2009-01-03T19:15:05+01:00\",  \"Nonce\": 2083236893,  \"HashMerkelRoot\": \"4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b\",  \"Bits\": \"00000000ffff0000000000000000000000000000000000000000000000000000\",  \"Difficulty\": 1.0}");
                /////
            }
        }

        //[DebuggerHidden]
        private void AssertWhatIsIt(ServerTester tester, string data, string expected)
        {
            var actual = tester.SendGet<string>("whatisit/" + data);
            if(expected == null)
            {
                Assert.Equal("\"Good question Holmes !\"", actual);
            }
            else
            {
                expected = ToCamel(expected);

                var actualJObj = JObject.Parse(actual);
                var prop = actualJObj.Property("firstSeen"); //Change non deterministic data
                if(prop != null)
                    prop.Value = new JValue(0);
                actual = actualJObj.ToString().Replace("\r\n", "").Replace("\"", "\\\"");
                expected = JObject.Parse(expected).ToString().Replace("\r\n", "").Replace("\"", "\\\"");
                Assert.Equal(expected, actual);
            }
        }

        private string ToCamel(string actual)
        {
            var data = JObject.Parse(actual);
            return ToCamel(data).ToString();

        }

        private static JObject ToCamel(JObject data)
        {
            foreach(var prop in data.Properties().ToList())
            {
                var name = prop.Name;
                var first = prop.Name.Substring(0, 1).ToLowerInvariant();
                name = first + name.Substring(1);
                if(prop.Value is JObject)
                    ToCamel((JObject)prop.Value);
                prop.Replace(new JProperty(name, prop.Value));
            }
            return data;
        }
    }
}
