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
using QBitNinja.Models;
using Xunit;
using System.Threading;
using NBitcoin.OpenAsset;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO.IsolatedStorage;
using System.IO;

namespace QBitNinja.Tests
{
    public class ServerTester : IDisposable
    {
        public static ServerTester Create([CallerMemberName]string ns = null)
        {
            return new ServerTester(ns);
        }

        public QBitNinjaConfiguration Configuration
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
            Address = "http://localhost:" + FindFreePort() + "/";
            Configuration = QBitNinjaConfiguration.FromConfiguration();
			Configuration.Indexer.Network = Network.RegTest;
            Configuration.Indexer.StorageNamespace = ns;
            Stopwatch watch = new Stopwatch();
            var server = WebApp.Start(Address, appBuilder =>
            {
                watch.Start();
                var config = new HttpConfiguration
                {
                    IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always
                };
                var setup = ShouldSetup(ns);
                WebApiConfig.Register(config, Configuration, setup);
                if(setup)
                {
                    SetShouldSetup(ns);
                }
                else
                {
                    //This at least should be setup
                    Configuration.Topics.EnsureSetupAsync().Wait();
                }
                _resolver = (QBitNinjaDependencyResolver)config.DependencyResolver;
                appBuilder.UseWebApi(config);

            });
            _disposables.Add(server);
            ChainBuilder = new ChainBuilder(this);
            _resolver.Get<ConcurrentChain>(); //So ConcurrentChain load
            watch.Stop();
        }

        private bool ShouldSetup(string ns)
        {
            DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            return !dir.GetFiles().Where(f => f.Name == "CACHED" + ns).Any();
        }

        public static void ClearShouldSetup()
        {
            DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            foreach(var file in dir.GetFiles().Where(f => f.Name.StartsWith("CACHED")))
            {
                file.Delete();
            }
        }

        private void SetShouldSetup(string ns)
        {
            DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            FileInfo fs = new FileInfo(Path.Combine(dir.FullName, "CACHED" + ns));
            try
            {
                fs.Create().Close();
            }
            catch
            {
            }
        }


        public static ushort FindFreePort()
        {
            while(true)
            {
                var port = (ushort)RandomUtils.GetUInt32();
                try
                {
                    TcpListener tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
                    tcpListener.Start();
                    tcpListener.Stop();
                    return port;
                }
                catch
                {
                }
            }
        }
        public bool CleanTable
        {
            get;
            set;
        }

        QBitNinjaDependencyResolver _resolver;

        #region IDisposable Members

        public void Dispose()
        {
            if(CleanTable)
            {

                var tasks = new List<Task>();
                tasks.Add(CleanAsync(Configuration.Indexer.GetBlocksContainer()));
                tasks.Add(CleanAsync(Configuration.Indexer.CreateTableClient()));
                tasks.AddRange(Configuration.Topics.All.Select(o => o.DeleteAsync()));

                Task.WaitAll(tasks.ToArray());

            }

            foreach(var dispo in _disposables)
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
            if(typeof(TResponse) == typeof(byte[]))
                return (TResponse)(object)response.Content.ReadAsByteArrayAsync().Result;
            if(typeof(string) == typeof(TResponse))
                return (TResponse)(object)response.Content.ReadAsStringAsync().Result;
            return response.Content.ReadAsAsync<TResponse>(new[] { Serializer.JsonMediaTypeFormatter }).Result;
        }

        //[DebuggerHidden]
        public TResponse Send<TResponse>(HttpMethod method, string relativeAddress, object body = null)
        {
            var seria = ((JsonMediaTypeFormatter)Serializer.JsonMediaTypeFormatter).SerializerSettings;
            seria.Formatting = Formatting.Indented;
            if(body != null)
            {
                var s = JsonConvert.SerializeObject(body, seria);
            }
            HttpClient client = new HttpClient();
            var response = client.SendAsync(new HttpRequestMessage(method, Address + relativeAddress)
            {
                Content = body == null ? null : new ObjectContent(body.GetType(), body, Serializer.JsonMediaTypeFormatter)
            }).Result;
            response.EnsureSuccessStatusCode();
            if(typeof(TResponse) == typeof(byte[]))
                return (TResponse)(object)response.Content.ReadAsByteArrayAsync().Result;
            if(typeof(string) == typeof(TResponse))
                return (TResponse)(object)response.Content.ReadAsStringAsync().Result;
            var result = response.Content.ReadAsAsync<TResponse>(new[] { Serializer.JsonMediaTypeFormatter }).Result;
            if(result != null)
            {
                var s = JsonConvert.SerializeObject(result, seria);
            }
            return result;
        }

        private static async Task CleanAsync(CloudBlobContainer cloudBlobContainer)
        {

            if(!await cloudBlobContainer.ExistsAsync().ConfigureAwait(false))
                return;
            var deletes = cloudBlobContainer.ListBlobs(useFlatBlobListing: true)
                .OfType<ICloudBlob>()
                .Select(async b =>
                {
                    bool breaklease = false;
                    try
                    {
                        await b.DeleteAsync().ConfigureAwait(false);
                        return;
                    }
                    catch(StorageException ex)
                    {
                        if(ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == 412)
                        {
                            breaklease = true;
                        }
                    }
                    if(breaklease)
                    {
                        await b.BreakLeaseAsync(TimeSpan.Zero).ConfigureAwait(false);
                        await b.DeleteAsync().ConfigureAwait(false);
                    }
                })
                .ToArray();

            await Task.WhenAll(deletes).ConfigureAwait(false);
        }

        private Task CleanAsync(CloudTableClient client)
        {
            var deletes = client.ListTables()
                .Where(table => table.Name.StartsWith(Configuration.Indexer.StorageNamespace, StringComparison.InvariantCultureIgnoreCase))
                .SelectMany(table => table.ExecuteQuery(new TableQuery()).Select(e => new
                {
                    Entity = e,
                    Table = table
                }))
                .Select(async table =>
                {
                    try
                    {
                        await table.Table.ExecuteAsync(TableOperation.Delete(table.Entity));
                    }
                    catch(StorageException ex)
                    {
                        if(ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == 404)
                            return;
                        throw;
                    }
                })
                .ToArray();
            return Task.WhenAll(deletes);
        }


        public void UpdateServerChain(bool insist = false)
        {
            if(!_resolver.UpdateChain())
            {
                if(insist)
                {
                    for(int i = 0; i < 5; i++)
                    {
                        Thread.Sleep(100);
                        if(_resolver.UpdateChain())
                            return;
                    }
                }
                Assert.True(false, "Chain should have updated");
            }
        }

        public void AssertTotal(IDestination dest, Money total)
        {
            var address = dest.ScriptPubKey.GetDestinationAddress(Network.TestNet).ToColoredAddress();
            var summary = SendGet<BalanceSummary>("balances/" + address + "/summary");
            Assert.Equal(summary.UnConfirmed.Amount + summary.Confirmed.Amount - summary.Immature.Amount, total);

            var balances = SendGet<BalanceModel>("balances/" + address);
            var actual = balances.Operations.SelectMany(o => o.ReceivedCoins).OfType<Coin>().Select(c => c.Amount).Sum()
                        -
                        balances.Operations.SelectMany(o => o.SpentCoins).OfType<Coin>().Select(c => c.Amount).Sum();
            Assert.Equal(actual, total);
        }

        public void AssertTotal(IDestination dest, IMoney total)
        {
            var zero = total.Sub(total);
            var address = dest.ScriptPubKey.GetDestinationAddress(Network.TestNet).ToColoredAddress();
            var summary = SendGet<BalanceSummary>("balances/" + address + "/summary");
            Assert.Equal(total, SelectAmount(zero, summary.UnConfirmed).Add(SelectAmount(zero, summary.Confirmed)).Add(SelectAmount(zero, summary.Immature)));

            var balances = SendGet<BalanceModel>("balances/" + address);
            var actual = balances.Operations.SelectMany(o => o.ReceivedCoins).Select(c => SelectAmount(zero, c))
                        .Sum(zero)
                        .Sub(
                        balances.Operations.SelectMany(o => o.SpentCoins).Select(c => SelectAmount(zero, c))
                        .Sum(zero));
            Assert.Equal(total, actual);
        }

        private IMoney SelectAmount(IMoney zero, ICoin coin)
        {
            var asset = zero is AssetMoney ? ((AssetMoney)zero).Id : null;
            if(asset == null)
            {
                var c = coin as Coin;
                if(c == null)
                    return zero;
                return c.Amount;
            }
            else
            {
                var cc = coin as ColoredCoin;
                if(cc == null)
                    return zero;
                if(cc.AssetId != asset)
                    return zero;
                return cc.Amount;
            }
        }

        private IMoney SelectAmount(IMoney zero, BalanceSummaryDetails balanceSummaryDetails)
        {
            var cc = zero as AssetMoney;
            if(cc == null)
                return balanceSummaryDetails.Amount;
            var assetDetails = balanceSummaryDetails.Assets.FirstOrDefault(a => a.Asset.AssetId == cc.Id);
            if(assetDetails == null)
                return zero;
            return new AssetMoney(assetDetails.Asset, assetDetails.Quantity);
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

        public ListenerTester CreateListenerTester(bool nodeOnly = false)
        {
            return new ListenerTester(this, nodeOnly);
        }

        public NotificationTester CreateNotificationServer()
        {
            var tester = new NotificationTester();
            _disposables.Add(tester);
            return tester;
        }
    }
}
