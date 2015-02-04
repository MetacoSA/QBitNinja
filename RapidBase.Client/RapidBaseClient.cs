using NBitcoin;
using Newtonsoft.Json;
using RapidBase.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
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


        public Task<BalanceModel> GetBalance(IDestination dest)
        {
            var address = AssertAddress(dest);
            return Get<BalanceModel>("balances/" + address);
        }
        public Task<BalanceSummary> GetBalanceSummary(IDestination dest)
        {
            var address = AssertAddress(dest);
            return Get<BalanceSummary>("balances/" + address + "/summary");
        }
        public Task<BalanceModel> GetBalance(string wallet, bool colored = false)
        {
            if (wallet == null)
                throw new ArgumentNullException("wallet");
            return Get<BalanceModel>("wallets/" + wallet + "/balance?colored=" + colored);
        }
        public Task<BalanceSummary> GetBalanceSummary(string wallet)
        {
            if (wallet == null)
                throw new ArgumentNullException("wallet");
            return Get<BalanceSummary>("wallets/" + wallet + "/summary");
        }

        public Task<WalletModel> CreateWallet(string wallet)
        {
            if (wallet == null)
                throw new ArgumentNullException("wallet");
            return Post<WalletModel>("wallets", new WalletModel()
            {
                Name = wallet
            });
        }

        private string AssertAddress(IDestination dest)
        {
            if (dest == null)
                throw new ArgumentNullException("address");
            var address = dest.ScriptPubKey.GetDestinationAddress(Network);
            if (address == null)
                throw new ArgumentException("address does not represent a valid bitcoin address", "address");
            if (dest is BitcoinColoredAddress)
                return dest.ToString();
            return address.ToString();
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

        private Task<T> Get<T>(string relativePath, params object[] parameters)
        {
            return Send<T>(HttpMethod.Get, null, relativePath, parameters);
        }

        private async Task<T> Send<T>(HttpMethod method, object body, string relativePath, params object[] parameters)
        {
            var uri = GetFullUri(relativePath, parameters);
            using (var client = new HttpClient())
            {
                var message = new HttpRequestMessage(method, uri);
                if (body != null)
                {
                    message.Content = new StringContent(Serializer.ToString(body), Encoding.UTF8, "application/json");
                }
                var result = await client.SendAsync(message).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                    return default(T);
                if (!result.IsSuccessStatusCode)
                {
                    string error = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(error))
                    {
                        try
                        {

                            var errorObject = Serializer.ToObject<RapidBaseError>(error);
                            throw new RapidBaseException(errorObject);
                        }
                        catch (JsonSerializationException)
                        {
                        }
                    }
                }
                result.EnsureSuccessStatusCode();
                var str = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                return Serializer.ToObject<T>(str);
            }
        }

        private Task<T> Post<T>(string relativePath, object content)
        {
            return Send<T>(HttpMethod.Post, content, relativePath);
        }


        public Task<GetTransactionResponse> GetTransaction(uint256 transactionId)
        {
            return Get<GetTransactionResponse>("transactions/" + transactionId);
        }

        public async Task<bool> CreateWalletIfNotExists(string name)
        {
            try
            {
                await CreateWallet(name).ConfigureAwait(false);
                return true;
            }
            catch (RapidBaseException ex)
            {
                if (ex.StatusCode == 409)
                    return false;
                throw;
            }
        }


        public Task<WalletAddress> AddAddress(string walletName, BitcoinAddress address, Script redeem = null, bool mergePast = true)
        {
            return AddAddress(walletName, new InsertWalletAddress()
            {
                Address = new WalletAddress()
                {
                    Address = address,
                    RedeemScript = redeem
                },
                MergePast = mergePast
            });
        }

        public Task<WalletAddress> AddAddress(string walletName, InsertWalletAddress address)
        {
            return Post<WalletAddress>("wallets/" + walletName + "/addresses", address);
        }

        public Task<bool> AddAddressIfNotExists(string walletName, Script redeemScript, bool mergePast = true)
        {
            return AddAddressIfNotExists(walletName, redeemScript.Hash, redeemScript, mergePast);
        }
        public Task<bool> AddAddressIfNotExists(string walletName, IDestination dest, Script redeem = null, bool mergePast = true)
        {
            var address = AssertAddress(dest);
            return AddAddressIfNotExists(walletName, new InsertWalletAddress()
            {
                Address = new WalletAddress()
                {
                    Address = address,
                    RedeemScript = redeem
                },
                MergePast = mergePast
            });
        }

        public async Task<bool> AddAddressIfNotExists(string walletName, InsertWalletAddress address)
        {
            try
            {
                await AddAddress(walletName, address).ConfigureAwait(false);
                return true;
            }
            catch (RapidBaseException ex)
            {
                if (ex.StatusCode == 409)
                    return false;
                throw;
            }
        }

        public Task<HDKeySet> AddKeySet(string walletName, HDKeySet keyset)
        {
            return Post<HDKeySet>("wallets/" + walletName + "/keysets", keyset);
        }

        public async Task<bool> AddKeySetIfNotExists(string walletName, HDKeySet keyset)
        {
            try
            {
                await Post<HDKeySet>("wallets/" + walletName + "/keysets", keyset).ConfigureAwait(false);
                return true;
            }
            catch (RapidBaseException ex)
            {
                if (ex.StatusCode == 409)
                    return false;
                throw;
            }
        }
    }
}
