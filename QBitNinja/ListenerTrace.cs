using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja
{
    internal static class Logs
    {
		public static void Configure(ILoggerFactory logger)
		{
			Main = logger.CreateLogger("QBitNinja"); 
		}

		public static ILogger Main
		{
			get; set;
		} = NullLogger.Instance;
    }
}
