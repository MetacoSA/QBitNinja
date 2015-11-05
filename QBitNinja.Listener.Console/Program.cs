using CommandLine;
using CommandLine.Text;
using Microsoft.Owin.Hosting;
using NBitcoin;
using QBitNinja.Notifications;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Owin;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Runtime.ExceptionServices;

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

        [Option("Web", HelpText = "Host QBit API in process", Required = false, DefaultValue = false)]
        public bool Web
        {
            get;
            set;
        }
        [Option("Port", HelpText = "The port used by the QBit API", Required = false, DefaultValue = 80)]
        public int Port
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

                List<IDisposable> dispo = new List<IDisposable>();
                List<Task> running = new List<Task>();
                try
                {

                    if (options.Listen)
                    {
                        QBitNinjaNodeListener listener = new QBitNinjaNodeListener(conf);
                        dispo.Add(listener);
                        listener.Listen();
                        running.Add(listener.Running);
                    }

                    if (options.Web)
                    {
                        System.Console.WriteLine("Trying to listen on http://*:" + options.Port + "/");
                        var server = WebApp.Start("http://*:" + options.Port, appBuilder =>
                        {
                            var config = new HttpConfiguration();
                            var qbit = QBitNinjaConfiguration.FromConfiguration();
                            qbit.EnsureSetup();
                            WebApiConfig.Register(config, qbit);
                            UpdateChainListener listener = new UpdateChainListener();
                            dispo.Add(listener);
                            listener.Listen(config);
                            appBuilder.UseWebApi(config);
                            running.Add(new TaskCompletionSource<int>().Task);
                        });
                        dispo.Add(server);
                        System.Console.WriteLine("Server started");
                        Process.Start("http://localhost:" + options.Port + "/blocks/tip");
                    }

                    if (running.Count != 0)
                    {
                        try
                        {
                            running.Add(WaitInput());
                            Task.WaitAny(running.ToArray());
                        }
                        catch (AggregateException aex)
                        {
                            ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
                            throw;
                        }
                    }
                }
                finally
                {
                    foreach(var d in dispo)
                        d.Dispose();
                }

            }
        }

        private static Task WaitInput()
        {
            return Task.Factory.StartNew(() =>
            {
                System.Console.WriteLine("Hit a key to stop...");
                System.Console.ReadLine();
            });
        }
    }
}
