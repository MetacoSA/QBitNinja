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
    public class WalletClient
    {
        public WalletClient(RapidBaseClient client, string walletName)
        {
            if (walletName == null)
                throw new ArgumentNullException("walletName");
            if (client == null)
                throw new ArgumentNullException("client");
            Client = client;
            WalletName = walletName;
        }

        public string WalletName
        {
            get;
            private set;
        }
        public RapidBaseClient Client
        {
            get;
            private set;
        }

        public Task<WalletModel> Create()
        {
            return Client.CreateWallet(WalletName);
        }
        public Task<bool> CreateIfNotExists()
        {
            return Client.CreateWalletIfNotExists(WalletName);
        }
        public Task<BalanceModel> GetBalance()
        {
            return Client.GetBalance(WalletName);
        }

        public Task<BalanceSummary> GetBalanceSummary()
        {
            return Client.GetBalanceSummary(WalletName);
        }

        public Task<bool> AddAddressIfNotExists(Script redeemScript, bool mergePast = true)
        {
            return Client.AddAddressIfNotExists(WalletName, redeemScript, mergePast);
        }
        public Task<bool> AddAddressIfNotExists(IDestination dest, Script redeem = null, bool mergePast = true)
        {
            return Client.AddAddressIfNotExists(WalletName, dest, redeem, mergePast);
        }
        public Task<bool> AddAddressIfNotExists(InsertWalletAddress address)
        {
            return Client.AddAddressIfNotExists(WalletName, address);
        }

        public Task<WalletAddress> AddAddress(Script redeemScript, bool mergePast = true)
        {
            return Client.AddAddress(WalletName, redeemScript, mergePast);
        }
        public Task<WalletAddress> AddAddress(IDestination dest, Script redeem = null, bool mergePast = true)
        {
            return Client.AddAddress(WalletName, dest, redeem, mergePast);
        }
        public Task<WalletAddress> AddAddress(InsertWalletAddress address)
        {
            return Client.AddAddress(WalletName, address);
        }

        public Task<bool> AddKeySetIfNotExists(HDKeySet keyset)
        {
            return Client.AddKeySetIfNotExists(WalletName, keyset);
        }
        public Task<HDKeySet> AddKeySet(HDKeySet keyset)
        {
            return Client.AddKeySet(WalletName, keyset);
        }
    }
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

        public bool Colored
        {
            get;
            set;
        }

        public Uri BaseAddress
        {
            get;
            private set;
        }

        public WalletClient GetWalletClient(string wallet)
        {
            return new WalletClient(this, wallet);
        }

        public Task<BalanceModel> GetBalance(IDestination dest)
        {
            var address = AssertAddress(dest);
            return Get<BalanceModel>("balances/" + address + "?colored=" + Colored);
        }
        public Task<BalanceSummary> GetBalanceSummary(IDestination dest)
        {
            var address = AssertAddress(dest);
            return Get<BalanceSummary>("balances/" + address + "/summary?colored=" + Colored);
        }
        public Task<BalanceModel> GetBalance(string wallet)
        {
            if (wallet == null)
                throw new ArgumentNullException("wallet");
            return Get<BalanceModel>("wallets/" + wallet + "/balance?colored=" + Colored);
        }
        public Task<BalanceSummary> GetBalanceSummary(string wallet)
        {
            if (wallet == null)
                throw new ArgumentNullException("wallet");
            return Get<BalanceSummary>("wallets/" + wallet + "/summary?colored=" + Colored);
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

        private BitcoinAddress AssertAddress(IDestination dest)
        {
            if (dest == null)
                throw new ArgumentNullException("address");
            var address = dest.ScriptPubKey.GetDestinationAddress(Network);
            if (address == null)
                throw new ArgumentException("address does not represent a valid bitcoin address", "address");
            return address;
        }

        public Task<GetBlockResponse> GetBlock(BlockFeature blockFeature, bool headerOnly = false)
        {
            return Get<GetBlockResponse>("blocks/" + blockFeature + "?headerOnly=" + headerOnly);
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


        public Task<WalletAddress> AddAddress(string walletName, InsertWalletAddress address)
        {
            return Post<WalletAddress>("wallets/" + walletName + "/addresses", address);
        }

        public Task<WalletAddress> AddAddress(string walletName, IDestination dest, Script redeem, bool mergePast = false)
        {
            return AddAddress(walletName, new InsertWalletAddress()
            {
                Address = new WalletAddress()
                {
                    Address = AssertAddress(dest),
                    RedeemScript = redeem
                },
                MergePast = mergePast
            });
        }

        public Task<WalletAddress> AddAddress(string walletName, Script redeemScript, bool mergePast = false)
        {
            return AddAddress(walletName, redeemScript.Hash, redeemScript, mergePast);
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
