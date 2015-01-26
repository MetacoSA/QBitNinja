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
        public RapidBaseClient(Uri baseAddress)
        {
            if (baseAddress == null)
                throw new ArgumentNullException("baseAddress");
            BaseAddress = baseAddress;
        }

        public Uri BaseAddress
        {
            get;
            private set;
        }

        public Task<BalanceModel> GetBalance(BitcoinAddress address)
        {
            return Get<BalanceModel>("balances/" + address);
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
            var client = new HttpClient();
            var result = await client.GetAsync(uri).ConfigureAwait(false);
            if (result.StatusCode == HttpStatusCode.NotFound)
                return default(T);
            result.EnsureSuccessStatusCode();
            var str = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
            return Serializer.ToObject<T>(str);
        }

        public Task<GetTransactionResponse> GetTransaction(uint256 transactionId)
        {
            return Get<GetTransactionResponse>("transactions/" + transactionId);
        }
    }
}
