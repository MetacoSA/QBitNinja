using NBitcoin;
using NBitcoin.DataEncoders;
using RapidBase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace RapidBase.Tests
{
    public class Class1
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
                AssetEx.HttpError(404, () => tester.SendGet<GetTransactionResponse>("transactions/" + txId));
                ////

                //Not correctly formatted should return 400 (Bad request)
                AssetEx.HttpError(400, () => tester.SendGet<GetTransactionResponse>("transactions/000lol"));
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
                var bytes = tester.SendGet<byte[]>("rawtransactions/" + tx.GetHash());
                Assert.True(bytes.SequenceEqual(tx.ToBytes()));
                response = tester.SendGet<GetTransactionResponse>("transactions/" + txId);
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
                var response = tester.SendGet<byte[]>("rawblocks/" + block.GetHash());
                Assert.True(response.SequenceEqual(block.ToBytes()));

                //404 if not found
                AssetEx.HttpError(404, () => tester.SendGet<byte[]>("rawblocks/18179931ea977cc0030c7c3e3e4d457f384b9e00aee9d86e39fbff0c5d3f4c40"));
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
            }
        }
    }
}
