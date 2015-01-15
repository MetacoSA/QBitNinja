using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace RapidBase.Client.Tests
{
    public class Class1
    {
        [Fact]
        public void CanGetBalance()
        {
            var client = CreateClient();
            var balances = client.GetBalance(new BitcoinAddress("15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe")).Result;
            Assert.NotNull(balances);
            Assert.True(balances.Operations.Any(o => o.Amount == Money.Coins(0.02m)));
        }

        private RapidBaseClient CreateClient()
        {
            return new RapidBaseClient(new Uri("http://rapidbase-test.azurewebsites.net/"));
        }

    }
}
