using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Microsoft.AspNetCore.ResponseCompression;

namespace QBitNinja
{
	public class Startup
	{
		IHostingEnvironment _Env;
		private IConfiguration _Config;

		public Startup(IHostingEnvironment env, IConfiguration configuration)
		{
			_Env = env;
			_Config = configuration;
		}
		public void ConfigureServices(IServiceCollection services)
		{
			services.Configure<GzipCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Optimal);
			services.AddQBitNinja(_Config);
			services.AddMvc();
		}
		public void Configure(IApplicationBuilder app)
		{
			if(_Env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseQBitNinja();
			app.UseMvc(routes =>
			{
				routes.MapRoute(
					name: "default",
					template: "{controller=Help}/{action=Index}");
			});
		}
	}
}
