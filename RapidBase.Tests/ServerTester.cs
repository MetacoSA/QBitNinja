using Microsoft.Owin.Hosting;
using Owin;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Net.Http.Formatting;
using Microsoft.WindowsAzure.Storage;
using RapidBase.Models;
using Xunit;
using System.Threading;
using NBitcoin.OpenAsset;

namespace RapidBase.Tests
{
    public class ServerTester : IDisposable
    {
        public static ServerTester Create([CallerMemberName]string ns = null)
        {
            return new ServerTester(ns);
        }

        public RapidBaseConfiguration Configuration
        {
            get;
            set;
        }

        public ChainBuilder ChainBuilder
        {
            get;
            set;
        }

        internal readonly List<IDisposable> _disposables = new List<IDisposable>();


        public ServerTester(string ns)
        {
            CleanTable = true;
            Address = "http://localhost:" + (ushort)RandomUtils.GetUInt32() + "/";
            Configuration = RapidBaseConfiguration.FromConfiguration();
            Configuration.Indexer.StorageNamespace = ns;
            var server = WebApp.Start(Address, appBuilder =>
            {
                var config = new HttpConfiguration
                {
                    IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always
                };
                WebApiConfig.Register(config, Configuration);
                _resolver = (RapidBaseDependencyResolver)config.DependencyResolver;
                appBuilder.UseWebApi(config);
            });
            _disposables.Add(server);
            ChainBuilder = new ChainBuilder(this);
            _resolver.Get<ConcurrentChain>(); //So ConcurrentChain load
        }
        public bool CleanTable
        {
            get;
            set;
        }

        public CallbackTester CreateCallbackTester()
        {
            var tester = new CallbackTester();
            _disposables.Add(tester);
            return tester;
        }

        RapidBaseDependencyResolver _resolver;

        #region IDisposable Members

        public void Dispose()
        {
            if (CleanTable)
            {
                Clean(Configuration.Indexer.GetBlocksContainer());
                Clean(Configuration.Indexer.CreateTableClient());
            }
            foreach (var dispo in _disposables)
                dispo.Dispose();
        }

        #endregion

        public string Address
        {
            get;
            set;
        }

        [DebuggerHidden]
        public TResponse SendGet<TResponse>(string relativeAddress)
        {
            HttpClient client = new HttpClient();
            var response = client.GetAsync(Address + relativeAddress).Result;
            response.EnsureSuccessStatusCode();
            if (typeof(TResponse) == typeof(byte[]))
                return (TResponse)(object)response.Content.ReadAsByteArrayAsync().Result;
            if (typeof(string) == typeof(TResponse))
                return (TResponse)(object)response.Content.ReadAsStringAsync().Result;
            return response.Content.ReadAsAsync<TResponse>(new[] { Serializer.JsonMediaTypeFormatter }).Result;
        }

        [DebuggerHidden]
        public TResponse Send<TResponse>(HttpMethod method, string relativeAddress, object body = null)
        {
            HttpClient client = new HttpClient();
            var response = client.SendAsync(new HttpRequestMessage(method, Address + relativeAddress)
            {
                Content = body == null ? null : new ObjectContent(body.GetType(), body, Serializer.JsonMediaTypeFormatter)
            }).Result;
            response.EnsureSuccessStatusCode();
            if (typeof(TResponse) == typeof(byte[]))
                return (TResponse)(object)response.Content.ReadAsByteArrayAsync().Result;
            if (typeof(string) == typeof(TResponse))
                return (TResponse)(object)response.Content.ReadAsStringAsync().Result;
            return response.Content.ReadAsAsync<TResponse>(new[] { Serializer.JsonMediaTypeFormatter }).Result;
        }

        private static void Clean(CloudBlobContainer cloudBlobContainer)
        {
            if (!cloudBlobContainer.Exists())
                return;
            foreach (var blob in cloudBlobContainer.ListBlobs(useFlatBlobListing: true).OfType<ICloudBlob>())
            {
                try
                {
                    blob.Delete();
                }
                catch (StorageException ex)
                {
                    if (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == 412)
                    {
                        try
                        {

                            blob.BreakLease(TimeSpan.Zero);
                            blob.Delete();
                        }
                        catch
                        {
                            Debugger.Break();
                        }
                    }
                }
            }
        }

        private void Clean(CloudTableClient client)
        {
            foreach (var table in client.ListTables())
            {
                if (table.Name.StartsWith(Configuration.Indexer.StorageNamespace, StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (var entity in table.ExecuteQuery(new TableQuery()))
                    {
                        table.Execute(TableOperation.Delete(entity));
                    }
                }
            }
        }


        public void UpdateServerChain(bool insist = false)
        {
            if (!_resolver.UpdateChain())
            {
                if (insist)
                {
                    for (int i = 0 ; i < 5 ; i++)
                    {
                        Thread.Sleep(100);
                        if (_resolver.UpdateChain())
                            return;
                    }
                }
                Assert.True(false, "Chain should have updated");
            }
        }

        public void AssertTotal(IDestination dest, Money total)
        {
            var address = dest.ScriptPubKey.GetDestinationAddress(Network.TestNet);
            var summary = SendGet<BalanceSummary>("balances/" + address + "/summary");
            Assert.Equal(total, summary.UnConfirmed.Amount + summary.Confirmed.Amount - summary.Immature.Amount);

            var balances = SendGet<BalanceModel>("balances/" + address);
            var actual = balances.Operations.SelectMany(o => o.ReceivedCoins).Select(c => c.Amount).Sum()
                        -
                        balances.Operations.SelectMany(o => o.SpentCoins).Select(c => c.Amount).Sum();
            Assert.Equal(total, actual);
        }

        public void AssertTotal(IDestination dest, Money total, AssetId asset)
        {
            var address = dest.ScriptPubKey.GetDestinationAddress(Network.TestNet).ToColoredAddress();
            var summary = SendGet<BalanceSummary>("balances/" + address + "/summary");
            Assert.Equal(total, SelectAmount(asset, summary.UnConfirmed) + SelectAmount(asset, summary.Confirmed) - SelectAmount(asset, summary.Immature));

            var balances = SendGet<BalanceModel>("balances/" + address);
            var actual = balances.Operations.SelectMany(o => o.ReceivedCoins).Select(c => SelectAmount(asset, c)).Sum()
                        -
                        balances.Operations.SelectMany(o => o.SpentCoins).Select(c => SelectAmount(asset, c)).Sum();
            Assert.Equal(total, actual);
        }

        private Money SelectAmount(AssetId asset, ICoin coin)
        {
            if (asset == null)
            {
                var c = coin as Coin;
                if (c == null)
                    return Money.Zero;
                return c.Amount;
            }
            else
            {
                var cc = coin as ColoredCoin;
                if (cc == null)
                    return Money.Zero;
                if (cc.AssetId != asset)
                    return Money.Zero;
                return cc.Amount;
            }
        }

        private Money SelectAmount(AssetId asset, BalanceSummaryDetails balanceSummaryDetails)
        {
            if (asset == null)
                return balanceSummaryDetails.Amount;
            var assetDetails = balanceSummaryDetails.Assets.FirstOrDefault(a => a.Asset.AssetId == asset);
            if (assetDetails == null)
                return Money.Zero;
            return assetDetails.Quantity;
        }

        public ICoin[] GetUnspentCoins(IDestination dest)
        {
            return GetUnspentCoins(dest.ScriptPubKey);
        }

        public ICoin[] GetUnspentCoins(Script dest)
        {
            var balances = SendGet<BalanceModel>("balances/" + dest.GetDestinationAddress(Network.TestNet).ToColoredAddress());

            var spent = balances.Operations.SelectMany(o => o.SpentCoins).Select(c => c.Outpoint).ToDictionary(c => c);
            var received = balances.Operations.SelectMany(o => o.ReceivedCoins);

            var unspent = received.Where(c => !spent.ContainsKey(c.Outpoint)).ToArray();
            return unspent;
        }

        public ListenerTester CreateListenerTester()
        {
            return new ListenerTester(this);
        }
    }
}
