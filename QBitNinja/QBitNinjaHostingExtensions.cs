using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBitcoin.Indexer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace QBitNinja
{
    public static class QBitNinjaHostingExtensions
    {
		public static void AddQBitNinja(this IServiceCollection services, IConfiguration configuration)
		{
			var qbit = QBitNinjaConfiguration.FromConfiguration(configuration);
			services.AddSingleton<QBitNinjaConfiguration>(qbit);
			services.AddTransient(p => qbit.Indexer.CreateIndexerClient());
			services.AddSingleton<ConcurrentChain>(p => new ConcurrentChain(qbit.Indexer.Network));
			services.AddSingleton<IHostedService, UpdateChainListener>();
		}
		public static void UseQBitNinja(this IApplicationBuilder builder)
		{
			ServicePointManager.UseNagleAlgorithm = false;
			ServicePointManager.Expect100Continue = false;
			ServicePointManager.DefaultConnectionLimit = 1000;

			Logs.Configure(builder.ApplicationServices.GetRequiredService<ILoggerFactory>());
			IndexerTrace.Configure(builder.ApplicationServices.GetRequiredService<ILoggerFactory>());

			var mvcJson = builder.ApplicationServices.GetRequiredService<IOptions<MvcJsonOptions>>();
			var network = builder.ApplicationServices.GetRequiredService<Network>();
			Serializer.RegisterFrontConverters(mvcJson.Value.SerializerSettings, network);
			builder.UseMiddleware<QBitNinjaMiddleware>();
		}
    }
}
