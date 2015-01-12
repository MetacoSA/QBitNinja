using NBitcoin;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RapidBase
{
    public class NewBlockEvent
    {
        public uint256 BlockId
        {
            get;
            set;
        }
        public BlockHeader Header
        {
            get;
            set;
        }
        public int Height
        {
            get;
            set;
        }
    }
    public class BlockEventManager
    {
        public BlockEventManager(RapidBaseConfiguration conf)
        {
            Configuration = conf;
            Timeout = TimeSpan.FromMinutes(60);
        }
        public TimeSpan Timeout
        {
            get;
            set;
        }
        public RapidBaseConfiguration Configuration
        {
            get;
            set;
        }

        public async Task NewBlock(ChainedBlock header)
        {
            await NewBlock(new NewBlockEvent()
            {
                BlockId = header.HashBlock,
                Header = header.Header,
                Height = header.Height
            });
        }

        public async Task NewBlock(NewBlockEvent evt)
        {

            var repo = Configuration.CreateCallbackRepository();
            var tasks = repo
                .GetCallbacks("onnewblock")
                .Select(c => Call(c, evt))
                .ToArray();
            await Task.WhenAll(tasks);
        }

        private async Task Call(CallbackRegistration registration, NewBlockEvent evt)
        {
            CancellationTokenSource cancel = new CancellationTokenSource();
            cancel.CancelAfter(Timeout);

            HttpClient client = new HttpClient();
            try
            {
                var url = registration.Url;
                await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, registration.Url)
                {
                    Content = new ObjectContent<NewBlockEvent>(evt, Serializer.JsonMediaTypeFormatter)
                }, cancel.Token);
            }
            catch (Exception) //TODO : Debug log + Persistent registration
            {
            }
        }

    }
}
