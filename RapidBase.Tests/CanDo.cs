using Microsoft.WindowsAzure.Storage.Auth;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer;
using Newtonsoft.Json.Linq;
using RapidBase.Controllers;
using RapidBase.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using Xunit;

namespace RapidBase.Tests
{
    public class CanDo
    {
        [Fact]
        public void CanGetTransaction()
        {
            using (var tester = ServerTester.Create())
            {
                var bob = new Key();
                var alice = new Key();

                //Not found should return 404 (Not found)
                var txId = new uint256(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)));
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
            }
        }



        [Fact]
        public void CanGetBlock()
        {
            using (var tester = ServerTester.Create())
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

        [Fact]
        public void CanSendCallback()
        {
            using (var tester = ServerTester.Create())
            {
                var callback = tester.CreateCallbackTester();
                tester.Send<CallbackRegistration>(HttpMethod.Post, "blocks/onnew", new CallbackRegistration(callback.Address));
                var eventManager = new BlockEventManager(tester.Configuration);
                var b = tester.ChainBuilder.EmitBlock();
                var unused = eventManager.NewBlock(tester.ChainBuilder.Chain.Tip);
                var result = callback.GetRequest<NewBlockEvent>();

                Assert.True(b.GetHash() == result.BlockId);
                Assert.True(result.Header.ToBytes().SequenceEqual(b.Header.ToBytes()));
                Assert.True(result.Height == 1);
            }
        }


        [Fact]
        public void CanUseCacheTable()
        {
            using (var tester = ServerTester.Create())
            {
                var table = tester.Configuration.GetChainCacheTable<string>("test");
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
            using (var tester = ServerTester.Create())
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
            using (var tester = ServerTester.Create())
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
        public void CanGetBalanceSummary()
        {
            using (var tester = ServerTester.Create())
            {
                var bob = new Key().GetBitcoinSecret(Network.TestNet);
                var alice = new Key().GetBitcoinSecret(Network.TestNet);

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

        //[Fact]
        //public void CanManageWallet()
        //{
        //    using (var tester = ServerTester.Create())
        //    {
        //    }
        //}

        [Fact]
        public void CanRegisterCallback()
        {
            using (var tester = ServerTester.Create())
            {
                var result = tester.Send<CallbackRegistration>(HttpMethod.Post, "blocks/onnew", new CallbackRegistration("http://google.com/test1"));
                Assert.True(result.Id != null);
                var result2 = tester.Send<CallbackRegistration>(HttpMethod.Post, "blocks/onnew", new CallbackRegistration("http://google.com/test1"));
                Assert.True(result.Id == result2.Id);
                tester.Send<CallbackRegistration>(HttpMethod.Post, "blocks/onnew", new CallbackRegistration("http://google.com/test2"));
                var results = tester.SendGet<CallbackRegistration[]>("blocks/onnew");
                Assert.True(results.Length == 2);
                tester.Send<string>(HttpMethod.Delete, "blocks/onnew/" + results[0].Id);
                AssertEx.HttpError(404, () => tester.Send<string>(HttpMethod.Delete, "blocks/onnew/" + results[0].Id));
            }
        }

        [Fact]
        public void CanGetBalance()
        {
            using (var tester = ServerTester.Create())
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
            using (var tester = ServerTester.Create())
            {
                tester.ChainBuilder.UploadBlock = true;
                var bob = new BitcoinSecret("KxMVn7SRNkWTfVa78UXCmsc6Kyp3aQZydnyzGzNBrRg2T9X1u4er");
                //whatisit/[address|txId|blockId|blockheader|base58|transaction|script|scriptbytes]

                //Can parse base58 secret (depends on MainNet)
                AssertWhatIsIt(
                    tester,
                    bob.ToString(),
                    "{  \"PublicKey\": {    \"Hex\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b\",    \"IsCompressed\": true,    \"Address\": {      \"IsP2SH\": false,      \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",      \"ScriptPubKey\": {        \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",        \"Address\": \"35Dwzds797NcNuP21oL2c2yqB62yM7TBrf\",        \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",        \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"      },      \"RedeemScript\": null,      \"PublicKey\": null,      \"Base58\": \"18GDK7Arwo1y7DnCab2QzheYXK6rqW8zzM\",      \"Type\": \"PUBKEY_ADDRESS\",      \"Network\": \"MainNet\"    },    \"p2shAddress\": {      \"IsP2SH\": true,      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"ScriptPubKey\": {        \"Hash\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",        \"Address\": \"36tbbjetMDKRFuvFb6CkXrfiymNhceuw5H\",        \"Raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",        \"Asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"      },      \"RedeemScript\": {        \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",        \"Address\": \"3NxUy3EJq1WPPkwq212y1KB8etpD4aTgCh\",        \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",        \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"      },      \"PublicKey\": null,      \"Base58\": \"3NxUy3EJq1WPPkwq212y1KB8etpD4aTgCh\",      \"Type\": \"SCRIPT_ADDRESS\",      \"Network\": \"MainNet\"    },    \"ScriptPubKey\": {      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"Address\": \"3NxUy3EJq1WPPkwq212y1KB8etpD4aTgCh\",      \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",      \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"    }  },  \"Base58\": \"KxMVn7SRNkWTfVa78UXCmsc6Kyp3aQZydnyzGzNBrRg2T9X1u4er\",  \"Type\": \"SECRET_KEY\",  \"Network\": \"MainNet\"}"
                    );
                //Can parse address (depends on TestNet)
                bob = bob.Key.GetBitcoinSecret(Network.TestNet);
                AssertWhatIsIt(
                    tester,
                    bob.GetAddress().ToString(),
                    "{  \"IsP2SH\": false,  \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",  \"ScriptPubKey\": {    \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",    \"Address\": \"2MvnA4No8kZsxah1ZgvwuDyy6PSF9DzErie\",    \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",    \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"  },  \"RedeemScript\": null,  \"PublicKey\": null,  \"Base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",  \"Type\": \"PUBKEY_ADDRESS\",  \"Network\": \"TestNet\"}"
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
                    "{  \"Transaction\": \"01000000000100e1f505000000001976a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac00000000\",  \"TransactionId\": \"2a73ffc6cedfa8f7d807f2448decde899ca924efead70ccccc5ab70f028492da\",  \"IsCoinbase\": false,  \"Block\": null,  \"SpentCoins\": [],  \"Fees\": -100000000}"
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
                    "{  \"AdditionalInformation\": {    \"BlockId\": \"fc6f54710fefb6c27cad94c6103ceec7b206a80b34b1e4458355d88d257337d5\",    \"BlockHeader\": \"0200000043497fd7f826957108f4a30fd9cec3aeba79972084e90ead01ea3309000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001000000\",    \"Height\": 1,    \"Confirmations\": 1  },  \"Block\": null}"
                    );
                /////

                //Can find block by height
                AssertWhatIsIt(
                    tester,
                    "1",
                    "{  \"AdditionalInformation\": {    \"BlockId\": \"fc6f54710fefb6c27cad94c6103ceec7b206a80b34b1e4458355d88d257337d5\",    \"BlockHeader\": \"0200000043497fd7f826957108f4a30fd9cec3aeba79972084e90ead01ea3309000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001000000\",    \"Height\": 1,    \"Confirmations\": 1  },  \"Block\": null}"
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
                    bob.Key.PubKey.ToHex(),
                    "{  \"Hex\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b\",  \"IsCompressed\": true,  \"Address\": {    \"IsP2SH\": false,    \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",    \"ScriptPubKey\": {      \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",      \"Address\": \"2MvnA4No8kZsxah1ZgvwuDyy6PSF9DzErie\",      \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",      \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"    },    \"RedeemScript\": null,    \"PublicKey\": null,    \"Base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",    \"Type\": \"PUBKEY_ADDRESS\",    \"Network\": \"TestNet\"  },  \"p2shAddress\": {    \"IsP2SH\": true,    \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",    \"ScriptPubKey\": {      \"Hash\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",      \"Address\": \"2MxSofUauxfpmThYoGDpd9oezC7asSEiNy5\",      \"Raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",      \"Asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"    },    \"RedeemScript\": {      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",      \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",      \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"    },    \"PublicKey\": null,    \"Base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",    \"Type\": \"SCRIPT_ADDRESS\",    \"Network\": \"TestNet\"  },  \"ScriptPubKey\": {    \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",    \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",    \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",    \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"  }}"
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
                    "{  \"IsP2SH\": false,  \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",  \"ScriptPubKey\": {    \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",    \"Address\": \"2MvnA4No8kZsxah1ZgvwuDyy6PSF9DzErie\",    \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",    \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"  },  \"RedeemScript\": null,  \"PublicKey\": {    \"Hex\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b\",    \"IsCompressed\": true,    \"Address\": {      \"IsP2SH\": false,      \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",      \"ScriptPubKey\": {        \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",        \"Address\": \"2MvnA4No8kZsxah1ZgvwuDyy6PSF9DzErie\",        \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",        \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"      },      \"RedeemScript\": null,      \"PublicKey\": null,      \"Base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",      \"Type\": \"PUBKEY_ADDRESS\",      \"Network\": \"TestNet\"    },    \"p2shAddress\": {      \"IsP2SH\": true,      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"ScriptPubKey\": {        \"Hash\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",        \"Address\": \"2MxSofUauxfpmThYoGDpd9oezC7asSEiNy5\",        \"Raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",        \"Asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"      },      \"RedeemScript\": {        \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",        \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",        \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",        \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"      },      \"PublicKey\": null,      \"Base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",      \"Type\": \"SCRIPT_ADDRESS\",      \"Network\": \"TestNet\"    },    \"ScriptPubKey\": {      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",      \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",      \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"    }  },  \"Base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",  \"Type\": \"PUBKEY_ADDRESS\",  \"Network\": \"TestNet\"}"
                    );
                //Should also find with the pub key hash
                AssertWhatIsIt(
                    tester,
                    "4fa965c94a53aaa0d87d1d05a826d77906ff5219",
                    "{  \"IsP2SH\": false,  \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",  \"ScriptPubKey\": {    \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",    \"Address\": \"2MvnA4No8kZsxah1ZgvwuDyy6PSF9DzErie\",    \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",    \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"  },  \"RedeemScript\": null,  \"PublicKey\": {    \"Hex\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b\",    \"IsCompressed\": true,    \"Address\": {      \"IsP2SH\": false,      \"Hash\": \"4fa965c94a53aaa0d87d1d05a826d77906ff5219\",      \"ScriptPubKey\": {        \"Hash\": \"26c1fdf631d1a846f2bf75a1c9c64c9dbf3922bc\",        \"Address\": \"2MvnA4No8kZsxah1ZgvwuDyy6PSF9DzErie\",        \"Raw\": \"76a9144fa965c94a53aaa0d87d1d05a826d77906ff521988ac\",        \"Asm\": \"OP_DUP OP_HASH160 4fa965c94a53aaa0d87d1d05a826d77906ff5219 OP_EQUALVERIFY OP_CHECKSIG\"      },      \"RedeemScript\": null,      \"PublicKey\": null,      \"Base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",      \"Type\": \"PUBKEY_ADDRESS\",      \"Network\": \"TestNet\"    },    \"p2shAddress\": {      \"IsP2SH\": true,      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"ScriptPubKey\": {        \"Hash\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",        \"Address\": \"2MxSofUauxfpmThYoGDpd9oezC7asSEiNy5\",        \"Raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",        \"Asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"      },      \"RedeemScript\": {        \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",        \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",        \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",        \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"      },      \"PublicKey\": null,      \"Base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",      \"Type\": \"SCRIPT_ADDRESS\",      \"Network\": \"TestNet\"    },    \"ScriptPubKey\": {      \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",      \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",      \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",      \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"    }  },  \"Base58\": \"mnnAcAFqkpTDtLFpJ9znpcrsPJhZfFUFQ5\",  \"Type\": \"PUBKEY_ADDRESS\",  \"Network\": \"TestNet\"}"
                    );
                /////

                //Can find redeem script if divulged
                tx = tester.ChainBuilder.EmitMoney(Money.Coins(1.0m), bob.Key.PubKey.ScriptPubKey.Hash);
                tx = new TransactionBuilder()
                        .AddKeys(bob)
                        .AddCoins(new Coin(tx, 0U))
                        .AddKnownRedeems(bob.Key.PubKey.ScriptPubKey)
                        .SendFees(Money.Coins(0.05m))
                        .SetChange(bob)
                        .BuildTransaction(true);
                tester.ChainBuilder.Broadcast(tx);
                AssertWhatIsIt(
                   tester,
                   bob.Key.PubKey.ScriptPubKey.GetScriptAddress(Network.TestNet).ToString(),
                   "{  \"IsP2SH\": true,  \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",  \"ScriptPubKey\": {    \"Hash\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",    \"Address\": \"2MxSofUauxfpmThYoGDpd9oezC7asSEiNy5\",    \"Raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",    \"Asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"  },  \"RedeemScript\": {    \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",    \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",    \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",    \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"  },  \"PublicKey\": null,  \"Base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",  \"Type\": \"SCRIPT_ADDRESS\",  \"Network\": \"TestNet\"}"
                   );
                //Should also find with the script hash
                AssertWhatIsIt(
                   tester,
                   "e947748c6687299740a448d524dc7aef830023a7",
                   "{  \"IsP2SH\": true,  \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",  \"ScriptPubKey\": {    \"Hash\": \"390950ca1e399f208c56d04cd23b5ad4ecefe0b8\",    \"Address\": \"2MxSofUauxfpmThYoGDpd9oezC7asSEiNy5\",    \"Raw\": \"a914e947748c6687299740a448d524dc7aef830023a787\",    \"Asm\": \"OP_HASH160 e947748c6687299740a448d524dc7aef830023a7 OP_EQUAL\"  },  \"RedeemScript\": {    \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",    \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",    \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",    \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"  },  \"PublicKey\": null,  \"Base58\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",  \"Type\": \"SCRIPT_ADDRESS\",  \"Network\": \"TestNet\"}"
                   );
                ////

                //Can decode script
                AssertWhatIsIt(
                  tester,
                  "21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac",
                  "{  \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",  \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",  \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",  \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"}"
                  );
                AssertWhatIsIt(
                  tester,
                  "025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG",
                  "{  \"Hash\": \"e947748c6687299740a448d524dc7aef830023a7\",  \"Address\": \"2NEWh2nALSU1jbYaNh8eqdGAPsF2NrKtL3b\",  \"Raw\": \"21025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4bac\",  \"Asm\": \"025300d86198673257a4d76c6b6e9012b0f3799fdbd7751065aa543b1615859e4b OP_CHECKSIG\"}"
                  );
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
            if (expected == null)
            {
                Assert.Equal("\"Good question Holmes !\"", actual);
            }
            else
            {
                expected = ToCamel(expected);
                actual = JObject.Parse(actual).ToString().Replace("\r\n", "").Replace("\"", "\\\"");
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
            foreach (var prop in data.Properties().ToList())
            {
                var name = prop.Name;
                var first = prop.Name.Substring(0, 1).ToLowerInvariant();
                name = first + name.Substring(1);
                if (prop.Value is JObject)
                    ToCamel((JObject)prop.Value);
                prop.Replace(new JProperty(name, prop.Value));
            }
            return data;
        }
    }
}
