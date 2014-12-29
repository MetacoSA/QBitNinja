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

                //Not found should return 404
                var txId = new uint256(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)));
                AssetEx.HttpError(404, () => tester.SendGet<GetTransactionResponse>("transactions/" + txId));
                ////

                //Found should return the correct transaction
                var tx = tester.ChainBuilder.EmitMoney("1.0", bob);
                txId = tx.GetHash();
                var response = tester.SendGet<GetTransactionResponse>("transactions/" + txId);
                Assert.NotNull(response);
                Assert.Equal(txId.ToString(), response.TransactionId.ToString());
                Assert.Equal(tx.ToString(), response.Transaction.ToString());
                ////
            }
        }
    }
}
