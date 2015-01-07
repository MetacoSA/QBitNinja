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

        readonly List<IDisposable> _disposables = new List<IDisposable>();


        public ServerTester(string ns)
        {
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
            Clean(Configuration.Indexer.GetBlocksContainer());
            Clean(Configuration.Indexer.CreateTableClient());
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


        public void UpdateServerChain()
        {
            if (!_resolver.UpdateChain())
                Assert.True(false, "Chain should have updated");
        }

        public void AssertTotal(BitcoinAddress address, Money total)
        {
            var summary = SendGet<BalanceSummary>("balances/" + address + "/summary");
            Assert.Equal(total, summary.UnConfirmed.Amount + summary.Confirmed.Amount - summary.Immature.Amount);

            var balances = SendGet<BalanceModel>("balances/" + address);
            var actual = balances.Operations.SelectMany(o => o.ReceivedCoins).Select(c => c.Amount).Sum()
                        -
                        balances.Operations.SelectMany(o => o.SpentCoins).Select(c => c.Amount).Sum();
            Assert.Equal(total, actual);
        }
    }
}
