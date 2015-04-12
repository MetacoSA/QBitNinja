using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja
{
    public static class ListenerTrace
    {
        static TraceSource _Source = new TraceSource("QBitNinja.Listener");
        public static void Error(string info, Exception ex)
        {
            _Source.TraceEvent(TraceEventType.Error, 0, info + "\r\n" + Utils.ExceptionToString(ex));
        }
        public static void Info(string info)
        {
            _Source.TraceInformation(info);
        }

        internal static void Verbose(string info)
        {
            _Source.TraceEvent(TraceEventType.Verbose, 0, info);
        }
    }
}
