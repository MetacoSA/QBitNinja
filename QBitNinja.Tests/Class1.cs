using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using QBitNinja.Client.Models;
using System;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace QBitNinja.Client.Tests
{
    public class Class1
    {
        [Fact]
        public void CanGetBalance()
        {
            var client = new QBitNinjaClient(Network.Main);
            var b = client.GetBlock(new BlockFeature(392690)).Result.Block;
            var blocksizeBefore = b.GetSerializedSize();
            foreach(var t in b.Transactions)
            {
                foreach(var input in t.Inputs)
                {
                    input.ScriptSig = Script.Empty;
                }
            }
            var blocksizeAfter = b.GetSerializedSize();

            var avgTxSizeBefore = blocksizeBefore / b.Transactions.Count;
            var avgTxSizeAfter = blocksizeAfter / b.Transactions.Count;

            var txPerBlocksBefore = (1000 * 1024) / avgTxSizeBefore;
            var txPerBlocksAfter = (750 * 1024) / avgTxSizeAfter;

            
            client = new QBitNinjaClient(Network.Main);
            var balance = client.GetBalance(new BitcoinPubKeyAddress("15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe")).Result;
            Assert.NotNull(balance);
            Assert.True(balance.Operations.Any(o => o.Amount == Money.Coins(0.02m)));

            var balanceSummary = client.GetBalanceSummary(new BitcoinPubKeyAddress("15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe")).Result;
            Assert.True(balanceSummary.Confirmed.TransactionCount > 60);


			//http://api.qbit.ninja/balances/1dice8EMZmqKvrGE4Qc9bUFf9PX3xaYDp?from=336410&until=336409
			var ops = client.GetBalanceBetween(new BalanceSelector(BitcoinAddress.Create("1dice8EMZmqKvrGE4Qc9bUFf9PX3xaYDp")), new BlockFeature(336410), new BlockFeature(336409)).Result;
			Assert.Equal(3, ops.Operations.Count);

			//http://api.qbit.ninja/balances/1dice8EMZmqKvrGE4Qc9bUFf9PX3xaYDp?from=177000
			ops = client.GetBalanceBetween(new BalanceSelector(BitcoinAddress.Create("1dice8EMZmqKvrGE4Qc9bUFf9PX3xaYDp")), new BlockFeature(177000), null).Result;
			Assert.Equal(4, ops.Operations.Count);
		}


        [Fact] //Will detect when I forget to change namespace for one type in the client package
        public void NoTypeFromQBitNinjaNS()
        {
            Assert.True(typeof(QBitNinjaClient).Assembly.GetTypes().All(t => t.Namespace != "QBitNinja"));
        }

        [Fact]
        public void temp()
        {
        }


        [Fact]
        public void CanTryBroadcast()
        {
            var client = new QBitNinjaClient(Network.Main);
            var result = client.Broadcast(new Transaction()
            {
                Inputs =
                {
                    new TxIn()
                    {
                        ScriptSig = new Script(Op.GetPushOp(RandomUtils.GetBytes(32)))
                    }
                }
            }).Result;

            Assert.False(result.Success);
            Assert.True(result.Error.ErrorCode == RejectCode.INVALID);
            Assert.True(result.Error.Reason == "bad-txns-vout-empty");
        }

        [Fact]
        public void CanDetectUncoherentNetwork()
        {
            try
            {

                var client = new QBitNinjaClient(Network.TestNet);
                var balance = client.GetBalance(new BitcoinPubKeyAddress("15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe")).Result;
                Assert.False(true, "Should have thrown");
            }
            catch (AggregateException ex)
            {
                var rex = (QBitNinjaException)ex.InnerException;
                if (rex == null)
                    Assert.False(true, "Should have thrown QBitNinjaException");
                Assert.Equal(400, rex.StatusCode);
            }
        }

        [Fact]
        public void CanManageWallet()
        {
            var client = new QBitNinjaClient(Network.Main);
            var wallet = client.GetWalletClient("temp-1Nicolas Dorier");
            wallet.CreateIfNotExists().Wait();
            wallet.CreateAddressIfNotExists(BitcoinAddress.Create("15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe")).Wait();
            wallet.CreateAddressIfNotExists(BitcoinAddress.Create("1KF8kUVHK42XzgcmJF4Lxz4wcL5WDL97PB")).Wait();

            var balance = wallet.GetBalance().Result;
            Assert.True(balance.Operations.Count > 70);

            var keyset = wallet.GetKeySetClient("main");

            keyset.CreateIfNotExists(new[] { new ExtKey().Neuter() }, path: KeyPath.Parse("1/2/3")).Wait();
            Assert.True(keyset.Delete().Result);
            Assert.False(keyset.Delete().Result);
            keyset.CreateIfNotExists(new[] { new ExtKey().Neuter() }, path: KeyPath.Parse("1/2/3")).Wait();
            var key = keyset.GetUnused(1).Result;

            var sets = wallet.GetKeySets().Result;
            Assert.True(sets.Length > 0);

            var model = client.GetWalletClient("temp-1Nicolas Dorier").Get().Result;
            Assert.NotNull(model);
            Assert.Null(client.GetWalletClient("ojkljdjlksj").Get().Result);

            Assert.True(wallet.GetAddresses().Result.Length > 1);
        }

        [Fact]
        public void CanGetBlock()
        {
            var client = new QBitNinjaClient(Network.Main);
            var block = client.GetBlock(new BlockFeature(SpecialFeature.Last), true).Result;
            Assert.NotNull(block);
            Assert.Null(block.Block);
            var height = block.AdditionalInformation.Height;
            block = client.GetBlock(new BlockFeature(SpecialFeature.Last)
            {
                Offset = -1
            }, true).Result;
            Assert.NotNull(block);
            Assert.Null(block.Block);
            Assert.True(block.AdditionalInformation.Height < height);
            block = client.GetBlock(new BlockFeature(SpecialFeature.Last)
            {
                Offset = 1
            }, true).Result;
            Assert.Null(block);
        }

        [Fact]
        public void CanGetTransaction()
        {
            var client = new QBitNinjaClient(Network.TestNet);
            var tx = client.GetTransaction(uint256.Parse("8412ef73d7c82cd9ac94004d20f79f1c1e6ecd93e340a4bacb735a24ec54c6b0")).Result;
            Assert.NotNull(tx);
            Assert.False(tx.ReceivedCoins.OfType<ColoredCoin>().Any());
            client.Colored = true;
            tx = client.GetTransaction(uint256.Parse("8412ef73d7c82cd9ac94004d20f79f1c1e6ecd93e340a4bacb735a24ec54c6b0")).Result;
            Assert.True(tx.ReceivedCoins.OfType<ColoredCoin>().Any());
        }

    }
}
