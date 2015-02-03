using NBitcoin;
using RapidBase.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace RapidBase.Client
{
    public class RapidBaseClient
    {
        public RapidBaseClient(Uri baseAddress, Network network = null)
        {
            if (baseAddress == null)
                throw new ArgumentNullException("baseAddress");
            Network = network ?? Network.Main;
            BaseAddress = baseAddress;
        }
        public Network Network
        {
            get;
            set;
        }

        public Uri BaseAddress
        {
            get;
            private set;
        }


        public Task<BalanceModel> GetBalance(IDestination address)
        {
            AssertAddress(address);
            return Get<BalanceModel>("balances/" + address);
        }
        public Task<BalanceSummary> GetBalanceSummary(IDestination address)
        {
            AssertAddress(address);
            return Get<BalanceSummary>("balances/" + address + "/summary");
        }
        public Task<BalanceModel> GetBalance(string wallet)
        {
            if (wallet == null)
                throw new ArgumentNullException("wallet");
            return Get<BalanceModel>("wallets/" + wallet);
        }
        public Task<BalanceSummary> GetBalanceSummary(string wallet)
        {
            if (wallet == null)
                throw new ArgumentNullException("wallet");
            return Get<BalanceSummary>("wallets/" + wallet + "/summary");
        }

        private void AssertAddress(IDestination dest)
        {
            if (dest == null)
                throw new ArgumentNullException("address");
            var address = dest.ScriptPubKey.GetDestinationAddress(Network);
            if (address == null)
                throw new ArgumentException("address does not represent a valid bitcoin address", "address");
        }

        public Task<GetBlockResponse> GetBlock(BlockFeature blockFeature, bool headerOnly = false)
        {
            return Get<GetBlockResponse>("blocks/" + blockFeature.ToString() + "?headerOnly=" + headerOnly);
        }

        private string GetFullUri(string relativePath, params object[] parameters)
        {
            relativePath = String.Format(relativePath, parameters);
            var uri = BaseAddress.AbsoluteUri;
            if (!uri.EndsWith("/"))
                uri += "/";
            uri += relativePath;
            return uri;
        }

        private async Task<T> Get<T>(string relativePath, params object[] parameters)
        {
            var uri = GetFullUri(relativePath, parameters);
            using (var client = new HttpClient())
            {
                var result = await client.GetAsync(uri).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                    return default(T);
                result.EnsureSuccessStatusCode();
                var str = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                return Serializer.ToObject<T>(str);
            }
        }

        public Task<GetTransactionResponse> GetTransaction(uint256 transactionId)
        {
            return Get<GetTransactionResponse>("transactions/" + transactionId);
        }
    }
}
