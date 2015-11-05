using Microsoft.Owin.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.Builder;
using Microsoft.Owin.Extensions;
using Owin;
using System.Threading;
using Xunit;
using System.IO;
using System.Collections.Concurrent;

namespace QBitNinja.Tests
{
    public class NotificationRequest
    {
        private IOwinContext context;
        TaskCompletionSource<int> _Complete;
        Stream _Body;
        public NotificationRequest(IOwinContext context)
        {
            this.context = context;
            _Complete = new TaskCompletionSource<int>();
            _Body = new MemoryStream();
            context.Request.Body.CopyTo(_Body);
            Task = _Complete.Task;
        }

        public NotificationRequest AssertReceived<T>(T obj)
        {
            var obj2 = GetBody<T>();
            Assert.Equal(Serializer.ToString(obj), Serializer.ToString(obj2));
            return this;
        }

        public T GetBody<T>()
        {
            _Body.Position = 0;
            var str = new StreamReader(_Body).ReadToEnd();
            var obj2 = Serializer.ToObject<T>(str);
            return obj2;
        }

        public void Complete(bool success)
        {
            if (!success)
                context.Response.StatusCode = 400;
            else
                context.Response.StatusCode = 200;
            _Complete.SetResult(0);
        }

        public Task Task
        {
            get;
            set;
        }
    }
    public class NotificationTester : IDisposable
    {
        BlockingCollection<NotificationRequest> _Requests = new BlockingCollection<NotificationRequest>(new ConcurrentQueue<NotificationRequest>());
        List<IDisposable> _Disposables = new List<IDisposable>();
        public NotificationTester()
        {
            Address = "http://localhost:" + ServerTester.FindFreePort() + "/";
            _Disposables.Add(WebApp.Start(Address, app =>
            {
                app.Run(context =>
                {
                    context.Response.ContentType = "text/plain";
                    var notif = new NotificationRequest(context);
                    _Requests.Add(notif);
                    return notif.Task;
                });
            }));
        }

        public string Address
        {
            get;
            set;
        }

        public NotificationRequest WaitRequest()
        {
            CancellationTokenSource tcs = new CancellationTokenSource();
            tcs.CancelAfter(35000);
            var data = _Requests.GetConsumingEnumerable(tcs.Token).FirstOrDefault();
            if (data == null || tcs.Token.IsCancellationRequested)
                Assert.True(false, "No request received");
            return data;
        }

        #region IDisposable Members

        public void Dispose()
        {
            foreach (var dispo in _Disposables)
            {
                dispo.Dispose();
            }
        }

        #endregion
    }
}
