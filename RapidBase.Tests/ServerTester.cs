using Microsoft.Owin.Hosting;
using Owin;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Runtime.CompilerServices;
using Xunit;
using System.Diagnostics;
using System.IO;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Net.Http.Formatting;
using Microsoft.WindowsAzure.Storage;
using System.Net.Http.Headers;
using System.Net;

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

        List<IDisposable> _Disposables = new List<IDisposable>();


        public ServerTester(string ns)
        {
            Address = "http://localhost:" + (ushort)RandomUtils.GetUInt32() + "/";
            Configuration = RapidBaseConfiguration.FromConfiguration();
            Configuration.Indexer.StorageNamespace = ns;
            var server = WebApp.Start(Address, appBuilder =>
            {
                var config = new HttpConfiguration();
                config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always;
                WebApiConfig.Register(config, Configuration);
                appBuilder.UseWebApi(config);
            });
            _Disposables.Add(server);
            ChainBuilder = new ChainBuilder(this);
        }

        #region IDisposable Members

        public void Dispose()
        {
            foreach (var dispo in _Disposables)
                dispo.Dispose();
        }

        #endregion

        public string Address
        {
            get;
            set;
        }

        public TResponse SendGet<TResponse>(string relativeAddress)
        {
            HttpClient client = new HttpClient();
            var response = client.GetAsync(Address + relativeAddress).Result;
            response.EnsureSuccessStatusCode();
            var mediaFormat = new JsonMediaTypeFormatter();
            Serializer.RegisterFrontConverters(mediaFormat.SerializerSettings);
            return response.Content.ReadAsAsync<TResponse>(new[] { mediaFormat }).Result;
        }
    }
}
