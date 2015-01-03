using NBitcoin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RapidBase.Tests
{
    public class CallbackTester : IDisposable
    {
        HttpListener _Listener;
        public CallbackTester()
        {
            Address = "http://localhost:" + (ushort)RandomUtils.GetUInt32() + "/";
            _Listener = new HttpListener();
            _Listener.Prefixes.Add(Address);
            _Listener.Start();
            _Listener.BeginGetContext(OnRequest, null);
        }

        void OnRequest(IAsyncResult ar)
        {
            try
            {
                var request = _Listener.EndGetContext(ar);
                _Requests.Add(request);
                _Listener.BeginGetContext(OnRequest, null);
            }
            catch
            {
            }
        }

        BlockingCollection<HttpListenerContext> _Requests = new BlockingCollection<HttpListenerContext>();
        HttpListenerContext _Request;
        public HttpListenerContext WaitRequest()
        {
            CancellationTokenSource cancel = new CancellationTokenSource();
            cancel.CancelAfter(5000);
            _Request = _Requests.GetConsumingEnumerable(cancel.Token).FirstOrDefault();
            return _Request;
        }

        public T GetRequest<T>()
        {
            var req = WaitRequest();
            var obj = Serializer.ToObject<T>(new StreamReader(req.Request.InputStream, Encoding.UTF8).ReadToEnd());
            CloseRequest();
            return obj;
        }


        public void CloseRequest()
        {
            _Request.Response.Close();
        }

        #region IDisposable Members

        public void Dispose()
        {
            _Listener.Stop();
        }

        #endregion

        public string Address
        {
            get;
            set;
        }
    }
}
