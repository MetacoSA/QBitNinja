using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Listener.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var conf = QBitNinjaConfiguration.FromConfiguration();
            conf.EnsureSetup();
            QBitNinjaListener listener = new QBitNinjaListener(conf);
            listener.Listen();
            listener.Wait();
        }
    }
}
