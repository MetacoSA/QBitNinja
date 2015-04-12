using NBitcoin;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace QBitNinja.Tests
{
    public class CallbackTester : IDisposable
    {
        readonly HttpListener _listener;
        public CallbackTester()
        {
            Address = "http://localhost:" + (ushort)RandomUtils.GetUInt32() + "/";
            _listener = new HttpListener();
            _listener.Prefixes.Add(Address);
            _listener.Start();
            _listener.BeginGetContext(OnRequest, null);
        }

        void OnRequest(IAsyncResult ar)
        {
            try
            {
                var request = _listener.EndGetContext(ar);
                _requests.Add(request);
                _listener.BeginGetContext(OnRequest, null);
            }
            catch
            {
            }
        }

        readonly BlockingCollection<HttpListenerContext> _requests = new BlockingCollection<HttpListenerContext>();
        HttpListenerContext _request;
        public HttpListenerContext WaitRequest()
        {
            CancellationTokenSource cancel = new CancellationTokenSource();
            cancel.CancelAfter(5000);
            _request = _requests.GetConsumingEnumerable(cancel.Token).FirstOrDefault();
            return _request;
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
            _request.Response.Close();
        }

        #region IDisposable Members

        public void Dispose()
        {
            _listener.Stop();
        }

        #endregion

        public string Address
        {
            get;
            set;
        }
    }
}
