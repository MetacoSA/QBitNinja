using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.Indexer;
using System.IO;
using Microsoft.Extensions.Hosting;

namespace QBitNinja
{

	public class UpdateChainListener : IHostedService, IDisposable
	{
		QBitNinjaConfiguration _QBit;
		ConcurrentChain _Chain;

		public UpdateChainListener(QBitNinjaConfiguration qbitConfiguration, ConcurrentChain chain)
		{
			_QBit = qbitConfiguration;
			_Chain = chain;
		}


		private static void SaveChainCache(ConcurrentChain chain, string cacheLocation)
		{
			if(string.IsNullOrEmpty(cacheLocation))
				return;
			try
			{
				var file = new FileInfo(cacheLocation);
				if(!file.Exists || (DateTime.UtcNow - file.LastWriteTimeUtc) > TimeSpan.FromDays(1))
					using(var fs = File.Open(cacheLocation, FileMode.Create))
					{
						chain.WriteTo(fs);
					}
			}
			catch //Don't care if fail
			{
			}
		}

		private static void LoadCache(ConcurrentChain chain, string cacheLocation)
		{
			if(string.IsNullOrEmpty(cacheLocation))
				return;
			try
			{
				var bytes = File.ReadAllBytes(cacheLocation);
				chain.Load(bytes);
			}
			catch //We don't care if it don't succeed
			{
			}
		}

		internal Timer Timer
		{
			get;
			set;
		}

		void Sync()
		{
			var client = _QBit.Indexer.CreateIndexerClient();
			var changes = client.GetChainChangesUntilFork(_Chain.Tip, false);
			try
			{
				changes.UpdateChain(_Chain);
			}
			catch(ArgumentException) //Happen when chain in table is corrupted
			{
				client.Configuration.GetChainTable().DeleteIfExistsAsync().GetAwaiter().GetResult();
				for(int i = 0; i < 20; i++)
				{
					try
					{
						if(client.Configuration.GetChainTable().CreateIfNotExistsAsync().GetAwaiter().GetResult())
							break;
					}
					catch
					{
					}
					Thread.Sleep(10000);
				}
				client.Configuration.CreateIndexer().IndexChain(_Chain);
			}
		}

		public void Listen()
		{
			LoadCache(_Chain, _QBit.LocalChain);

			Timer = new Timer(_ => UpdateChain());
			Timer.Change(0, (int)TimeSpan.FromSeconds(1).TotalMilliseconds);
		}

		public bool UpdateChain()
		{
			var client = _QBit.Indexer.CreateIndexerClient();
			var chain = _Chain;
			var oldTip = chain.Tip.HashBlock;
			var changes = client.GetChainChangesUntilFork(chain.Tip, false);
			changes.UpdateChain(chain);
			var newTip = chain.Tip.HashBlock;
			return newTip != oldTip;
		}

		#region IDisposable Members

		bool _Disposed;
		public void Dispose()
		{
			if(_Disposed)
				return;
			if(Timer != null)
				Timer.Dispose();
			_Disposed = true;
			SaveChainCache(_Chain, _QBit.LocalChain);
		}
		#endregion

		public Task StartAsync(CancellationToken cancellationToken)
		{
			Listen();
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			Dispose();
			return Task.CompletedTask;
		}

	}
}
