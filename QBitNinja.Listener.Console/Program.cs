using QBitNinja.Notifications;
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
            using (QBitNinjaNodeListener listener = new QBitNinjaNodeListener(conf))
            {
                listener.Listen();
                using (BlocksUpdater updater = new BlocksUpdater(conf))
                {
                    updater.Listen(listener.Chain);
                }
                listener.Wait();
            }
        }
    }
}
