using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase.Listener.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var conf = RapidBaseConfiguration.FromConfiguration();
            conf.EnsureSetup();
            RapidBaseListener listener = new RapidBaseListener(conf);
            listener.Listen();
            listener.Wait();
        }
    }
}
