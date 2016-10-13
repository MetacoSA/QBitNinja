using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
	public class ChainStatus
	{
		public ChainStatus()
		{
			
		}
		public BlockInformation LatestBlock
		{
			get;
			set;
		}
		
	}
}