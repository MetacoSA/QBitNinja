using CommandLine;
using CommandLine.Text;
using QBitNinja.Notifications;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.Listener.Console
{
    public class ListenerOptions
    {

        [Option("Listen", HelpText = "Listen the local node and index bitcoin transaction and blocks. Can run on several boxes at the same time.", Required = false, DefaultValue = false)]
        public bool Listen
        {
            get;
            set;
        }

        [Option("CancelInit", HelpText = "Cancel current initial indexation", Required = false, DefaultValue = false)]
        public bool CancelInit
        {
            get;
            set;
        }

        [Option("Init", HelpText = "Connect to the local node and index all bitcoin blocks/balances/transactions. Can run on several boxes at the same time.", Required = false, DefaultValue = false)]
        public bool Init
        {
            get;
            set;
        }

        string _Usage;
        [HelpOption('?', "help", HelpText = "Display this help screen.")]
        public string GetUsage()
        {
            if (_Usage == null)
            {
                _Usage = HelpText.AutoBuild(this, (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
            }
            return _Usage;
            //
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            var options = new ListenerOptions();
            if (args.Length == 0)
                System.Console.WriteLine(options.GetUsage());
            if (Parser.Default.ParseArguments(args, options))
            {
                var conf = QBitNinjaConfiguration.FromConfiguration();
                conf.EnsureSetup();
                if (options.CancelInit)
                {
                    var indexer = new InitialIndexer(conf);
                    indexer.Cancel();
                }
                if (options.Init)
                {
                    var indexer = new InitialIndexer(conf);
                    indexer.Run();
                }
                if (options.Listen)
                {
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
    }
}
