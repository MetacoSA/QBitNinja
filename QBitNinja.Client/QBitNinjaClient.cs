using NBitcoin;
using System.Linq;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NBitcoin.DataEncoders;
using System.Net.Http.Headers;
#if CLIENT
using QBitNinja.Client.JsonConverters;
using QBitNinja.Client.Models;
#else
using QBitNinja.JsonConverters;
using QBitNinja.Models;
#endif

namespace QBitNinja.Client
{
    public class KeySetClient
    {
        private readonly QBitNinjaClient _Client;
        public QBitNinjaClient Client
        {
            get
            {
                return _Client;
            }
        }

        private readonly WalletClient _Wallet;
        public WalletClient Wallet
        {
            get
            {
                return _Wallet;
            }
        }

        private readonly string _Name;
        public string Name
        {
            get
            {
                return _Name;
            }
        }
        public KeySetClient(WalletClient walletClient, string keySet)
        {
            if(keySet == null)
                throw new ArgumentNullException("keySet");
            if(walletClient == null)
                throw new ArgumentNullException("walletClient");
            _Name = keySet;
            _Wallet = walletClient;
            _Client = _Wallet.Client;
        }

        public Task<HDKeySet> Create(ExtPubKey[] keys, int signatureCount = 1, KeyPath path = null)
        {
            return Client.CreateKeySet(Wallet.Name, Name, keys, signatureCount, path);
        }

        public Task<HDKeySet> Create(HDKeySet keyset)
        {
            keyset.Name = Name;
            return Client.CreateKeySet(Wallet.Name, keyset);
        }

        public Task<bool> CreateIfNotExists(ExtPubKey[] keys, int signatureCount = 1, KeyPath path = null)
        {
            return Client.CreateKeySetIfNotExists(Wallet.Name, Name, keys, signatureCount, path);
        }

        public Task<bool> CreateIfNotExists(HDKeySet keyset)
        {
            keyset.Name = Name;
            return Client.CreateKeySetIfNotExists(Wallet.Name, keyset);
        }

        public Task<HDKeyData> GetUnused(int lookahead)
        {
            return Client.GetUnused(Wallet.Name, Name, lookahead);
        }

        public Task<bool> Delete()
        {
            return Client.DeleteKeySet(Wallet.Name, Name);
        }
    }
    public class WalletClient
    {
        public WalletClient(QBitNinjaClient client, string walletName)
        {
            if(walletName == null)
                throw new ArgumentNullException("walletName");
            if(client == null)
                throw new ArgumentNullException("client");
            Client = client;
            Name = walletName;
        }

        public string Name
        {
            get;
            private set;
        }
        public QBitNinjaClient Client
        {
            get;
            private set;
        }

        public Task<WalletModel> Create()
        {
            return Client.CreateWallet(Name);
        }
        public Task<bool> CreateIfNotExists()
        {
            return Client.CreateWalletIfNotExists(Name);
        }
        public Task<BalanceModel> GetBalance()
        {
            return Client.GetBalance(Name);
        }

        public Task<BalanceSummary> GetBalanceSummary()
        {
            return Client.GetBalanceSummary(Name);
        }

        public Task<bool> CreateAddressIfNotExists(Script redeemScript, bool mergePast = true)
        {
            return Client.CreateAddressIfNotExists(Name, redeemScript, mergePast);
        }
        public Task<bool> CreateAddressIfNotExists(IDestination dest, Script redeem = null, bool mergePast = true)
        {
            return Client.CreateAddressIfNotExists(Name, dest, redeem, mergePast);
        }
        public Task<bool> CreateAddressIfNotExists(InsertWalletAddress address)
        {
            return Client.CreateAddressIfNotExists(Name, address);
        }

        public Task<WalletAddress> CreateAddress(Script redeemScript, bool mergePast = true)
        {
            return Client.CreateAddress(Name, redeemScript, mergePast);
        }
        public Task<WalletAddress> CreateAddress(IDestination dest, Script redeem = null, bool mergePast = true)
        {
            return Client.CreateAddress(Name, dest, redeem, mergePast);
        }
        public Task<WalletAddress> CreateAddress(InsertWalletAddress address)
        {
            return Client.CreateAddress(Name, address);
        }

        public KeySetClient GetKeySetClient(string keySet)
        {
            return new KeySetClient(this, keySet);
        }

        public Task<KeySetData[]> GetKeySets()
        {
            return Client.GetKeySets(Name);
        }

        public Task<WalletModel> Get()
        {
            return Client.GetWallet(Name);
        }

        public Task<WalletAddress[]> GetAddresses()
        {
            return Client.GetAddresses(Name);
        }
    }
    public class QBitNinjaClient
    {
        /// <summary>
        /// Use qbit ninja public servers (api.qbit.ninja / tapi.qbit.ninja)
        /// </summary>
        /// <param name="network">The bitcoin network to use</param>
        public QBitNinjaClient(Network network)
        {
            if(network == null)
                throw new ArgumentNullException("network");
            Network = network;
            if(network == Network.Main)
                BaseAddress = new Uri("https://api.qbit.ninja/", UriKind.Absolute);
            if(network == Network.TestNet)
                BaseAddress = new Uri("https://tapi.qbit.ninja/", UriKind.Absolute);
            if(BaseAddress == null)
                throw new NotSupportedException("Network not supported");
        }
        public QBitNinjaClient(string baseAddress, Network network = null)
            : this(new Uri(baseAddress, UriKind.Absolute), network)
        {

        }
        public QBitNinjaClient(Uri baseAddress, Network network = null)
        {
            if(baseAddress == null)
                throw new ArgumentNullException("baseAddress");
            Network = network ?? Network.Main;
            BaseAddress = baseAddress;
        }
        public Network Network
        {
            get;
            set;
        }

        /// <summary>
        /// If true, requested balance will show colored coins. If null, only colored addresses will show colored balances. If false, no colored coin will be shown.
        /// </summary>
        public bool? Colored
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

        public Task<BroadcastResponse> Broadcast(Transaction transaction)
        {
            return Post<BroadcastResponse>("transactions", Encoders.Hex.EncodeData(transaction.ToBytes()));
        }


		public Task<BalanceModel> GetBalance(BalanceSelector dest, bool unspentOnly = false)
		{
			if(dest == null)
				throw new ArgumentNullException("dest");
			return Get<BalanceModel>("balances/" + EscapeUrlPart(dest.ToString()) + CreateParameters("unspentOnly", unspentOnly));
		}

		public Task<BalanceModel> GetBalance(string wallet, bool unspentOnly = false)
		{
			if(wallet == null)
				throw new ArgumentNullException("wallet");
			return GetBalance(new BalanceSelector(new WalletName(wallet)), unspentOnly);
		}

		public Task<BalanceModel> GetBalance(Script dest, bool unspentOnly = false)
		{
			if(dest == null)
				throw new ArgumentNullException("dest");
			return GetBalance(new BalanceSelector(dest), unspentOnly);
		}

		public Task<BalanceModel> GetBalance(IDestination dest, bool unspentOnly = false)
        {
			if(dest == null)
				throw new ArgumentNullException("dest");
			return GetBalance(new BalanceSelector(dest), unspentOnly);
		}

		public Task<BalanceModel> GetBalanceBetween(
			BalanceSelector dest,
			BlockFeature from = null,
			BlockFeature until = null,
			bool includeImmature = false,
			bool unspentOnly = false)
		{

			return Get<BalanceModel>("balances/" + EscapeUrlPart(dest.ToString())
				+ CreateParameters("unspentOnly", unspentOnly,
								   "until", until,
								   "from", from));
		}

		public Task<BalanceSummary> GetBalanceSummary(BalanceSelector dest)
		{
			if(dest == null)
				throw new ArgumentNullException("dest");
			return Get<BalanceSummary>("balances/" + EscapeUrlPart(dest.ToString()) + "/summary" + CreateParameters());
		}

		public Task<BalanceSummary> GetBalanceSummary(Script dest)
		{
			if(dest == null)
				throw new ArgumentNullException("dest");
			return GetBalanceSummary(new BalanceSelector(dest));
		}

		public Task<BalanceSummary> GetBalanceSummary(string wallet)
		{
			if(wallet == null)
				throw new ArgumentNullException("wallet");
			return GetBalanceSummary(new BalanceSelector(new WalletName(wallet)));
		}

		public Task<BalanceSummary> GetBalanceSummary(IDestination dest)
        {
			if(dest == null)
				throw new ArgumentNullException("dest");
			return GetBalanceSummary(new BalanceSelector(dest));
        }



		private string CreateParameters(params object[] parameters)
        {
            if(Colored != null)
            {
                parameters = parameters.Concat(new object[] { "colored", Colored.Value }).ToArray();
            }

            StringBuilder builder = new StringBuilder();
            for(int i = 0; i < parameters.Length - 1; i += 2)
            {
				if(parameters[i + 1] == null)
					continue;
                builder.Append(parameters[i].ToString() + "=" + parameters[i + 1].ToString() + "&");
            }
            if(builder.Length == 0)
                return "";
            var result = builder.ToString();
            return "?" + result.Substring(0, result.Length - 1);
        }              

        public Task<WalletModel> CreateWallet(string wallet)
        {
            if(wallet == null)
                throw new ArgumentNullException("wallet");
            return Post<WalletModel>("wallets", new WalletModel()
            {
                Name = wallet
            });
        }

		private IDestination AssertAddress(IDestination dest)
		{
			if(dest == null)
				throw new ArgumentNullException("address");
			var base58 = dest as IBitcoinString;
			var network = base58 == null ? Network : base58.Network;
			var address = dest.ScriptPubKey.GetDestinationAddress(network);
			if(address == null)
				throw new ArgumentException("address does not represent a valid bitcoin address", "address");
			if(dest is BitcoinColoredAddress)
				return (BitcoinColoredAddress)dest;
			return address;
		}

		public Task<GetBlockResponse> GetBlock(BlockFeature blockFeature, bool headerOnly = false, bool extended = false)
        {
            return Get<GetBlockResponse>("blocks/" + EscapeUrlPart(blockFeature.ToString()) + "?headerOnly=" + headerOnly + "&extended=" + extended);
        }

        private string GetFullUri(string relativePath, params object[] parameters)
        {
            relativePath = String.Format(relativePath, parameters ?? new object[0]);
            var uri = BaseAddress.AbsoluteUri;
            if(!uri.EndsWith("/"))
                uri += "/";
            uri += relativePath;
            return uri;
        }

        public Task<T> Get<T>(string relativePath, params object[] parameters)
        {
            return Send<T>(HttpMethod.Get, null, relativePath, parameters);
        }

        static HttpClient DefaultClient;
		HttpClient Client = DefaultClient;
		static QBitNinjaClient()
		{
			HttpClient client = CreateHttpClient(new HttpClientHandler() { UseCookies = false });
			DefaultClient = client;
		}

		public void SetHttpMessageHandler(HttpMessageHandler innerHandler)
		{
			Client = CreateHttpClient(innerHandler);
		}

		private static HttpClient CreateHttpClient(HttpMessageHandler innerHandler)
		{
			var client = new HttpClient(new DecompressionHandler(innerHandler));
			client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
			return client;
		}

		public async Task<T> Send<T>(HttpMethod method, object body, string relativePath, params object[] parameters)
        {
            var uri = GetFullUri(relativePath, parameters);   
			
            var message = new HttpRequestMessage(method, uri);
            if(body != null)
            {
                message.Content = new StringContent(Serializer.ToString(body, Network), Encoding.UTF8, "application/json");
            }
            var result = await Client.SendAsync(message).ConfigureAwait(false);
            if(result.StatusCode == HttpStatusCode.NotFound)
                return default(T);
            if(!result.IsSuccessStatusCode)
            {
                string error = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                if(!string.IsNullOrEmpty(error))
                {
                    try
                    {
                        var errorObject = Serializer.ToObject<QBitNinjaError>(error, Network);
                        if(errorObject.StatusCode != 0)
                            throw new QBitNinjaException(errorObject);
                    }
                    catch(JsonSerializationException)
                    {
                    }
                    catch(JsonReaderException)
                    {
                    }
                }
            }
            result.EnsureSuccessStatusCode();
            if(typeof(T) == typeof(byte[]))
                return (T)(object)await result.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var str = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
            if(typeof(T) == typeof(string))
                return (T)(object)str;
            return Serializer.ToObject<T>(str, Network);
        }

        public Task<T> Post<T>(string relativePath, object content)
        {
            return Send<T>(HttpMethod.Post, content, relativePath);
        }


        public Task<GetTransactionResponse> GetTransaction(uint256 transactionId)
        {
            return Get<GetTransactionResponse>("transactions/" + EscapeUrlPart(transactionId.ToString()) + CreateParameters());
        }

        public async Task<bool> CreateWalletIfNotExists(string name)
        {
            try
            {
                await CreateWallet(name).ConfigureAwait(false);
                return true;
            }
            catch(QBitNinjaException ex)
            {
                if(ex.StatusCode == 409)
                    return false;
                throw;
            }
        }


        public Task<WalletAddress> CreateAddress(string walletName, InsertWalletAddress address)
        {
            return Post<WalletAddress>("wallets/" + EscapeUrlPart(walletName) + "/addresses", address);
        }

        public Task<WalletAddress> CreateAddress(string walletName, IDestination dest, Script redeem, bool mergePast = false)
        {
            return CreateAddress(walletName, new InsertWalletAddress()
            {
                Address = AssertAddress(dest),
                RedeemScript = redeem,
                MergePast = mergePast
            });
        }

        public Task<WalletAddress> CreateAddress(string walletName, Script redeemScript, bool mergePast = false)
        {
            return CreateAddress(walletName, redeemScript.Hash, redeemScript, mergePast);
        }

        public Task<bool> CreateAddressIfNotExists(string walletName, Script redeemScript, bool mergePast = true)
        {
            return CreateAddressIfNotExists(walletName, redeemScript.Hash, redeemScript, mergePast);
        }
        public Task<bool> CreateAddressIfNotExists(string walletName, IDestination dest, Script redeem = null, bool mergePast = true)
        {
            var address = AssertAddress(dest);
            return CreateAddressIfNotExists(walletName, new InsertWalletAddress()
            {
                Address = address,
                RedeemScript = redeem,
                MergePast = mergePast
            });
        }

        public async Task<bool> CreateAddressIfNotExists(string walletName, InsertWalletAddress address)
        {
            try
            {
                await CreateAddress(walletName, address).ConfigureAwait(false);
                return true;
            }
            catch(QBitNinjaException ex)
            {
                if(ex.StatusCode == 409)
                    return false;
                throw;
            }
        }

        public Task<HDKeySet> CreateKeySet(string walletName, HDKeySet keyset)
        {
            return Post<HDKeySet>("wallets/" + EscapeUrlPart(walletName) + "/keysets", keyset);
        }

        public async Task<bool> CreateKeySetIfNotExists(string walletName, HDKeySet keyset)
        {
            try
            {
                await Post<HDKeySet>("wallets/" + EscapeUrlPart(walletName) + "/keysets", keyset).ConfigureAwait(false);
                return true;
            }
            catch(QBitNinjaException ex)
            {
                if(ex.StatusCode == 409)
                    return false;
                throw;
            }
        }

        public Task<bool> CreateKeySetIfNotExists(string wallet, string keyset, ExtPubKey[] keys, int signatureCount, KeyPath path)
        {
            return CreateKeySetIfNotExists(wallet, new HDKeySet()
            {
                Name = keyset,
                ExtPubKeys = keys.Select(k => k.GetWif(Network)).ToArray(),
                SignatureCount = signatureCount,
                Path = path
            });
        }

        public Task<HDKeySet> CreateKeySet(string wallet, string keyset, ExtPubKey[] keys, int signatureCount, KeyPath path)
        {
            return CreateKeySet(wallet, new HDKeySet()
            {
                Name = keyset,
                ExtPubKeys = keys.Select(k => k.GetWif(Network)).ToArray(),
                SignatureCount = signatureCount,
                Path = path
            });
        }

        public Task<HDKeyData> GetUnused(string wallet, string keyset, int lookahead)
        {
            return Get<HDKeyData>(BuildPath(wallet, keyset) + "/unused/" + lookahead, null);
        }

        private static string BuildPath(string wallet, string keyset)
        {
            return "wallets/" + EscapeUrlPart(wallet) + "/keysets/" + EscapeUrlPart(keyset);
        }

        public async Task<bool> DeleteKeySet(string wallet, string keyset)
        {
            var result = await Send<string>(HttpMethod.Delete, null, BuildPath(wallet, keyset), null).ConfigureAwait(false);
            return result != null;
        }

        public Task<KeySetData[]> GetKeySets(string wallet)
        {
            return Get<KeySetData[]>("wallets/" + EscapeUrlPart(wallet) + "/keysets");
        }

        public Task<WalletModel> GetWallet(string walletName)
        {
            return Get<WalletModel>("wallets/" + EscapeUrlPart(walletName));
        }

        public static string EscapeUrlPart(string str)
        {
            var path = System.Web.NBitcoin.HttpUtility.UrlEncode(str);
            if(path.Contains("?") || path.Contains("/"))
                throw new ArgumentException("Invalid character found in the path of the request ('?' or '/')");
            return path;
        }

        public Task<WalletAddress[]> GetAddresses(string walletName)
        {
            return Get<WalletAddress[]>("wallets/" + EscapeUrlPart(walletName) + "/addresses");
        }
    }
}
