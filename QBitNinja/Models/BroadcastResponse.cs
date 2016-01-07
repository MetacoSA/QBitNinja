using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
    public class BroadcastError
    {
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public RejectCode ErrorCode
        {
            get;
            set;
        }

        public string Reason
        {
            get;
            set;
        }
    }
    public class BroadcastResponse
    {
        public bool Success
        {
            get;
            set;
        }

        public BroadcastError Error
        {
            get;
            set;
        }
    }
}
