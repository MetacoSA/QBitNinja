using NBitcoin;

#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
	public class ExtendedBlockInformation
	{
		public int Size
		{
			get;
			set;
		}
		public int StrippedSize
		{
			get;
			set;
		}
		public int TransactionCount
		{
			get;
			set;
		}
		public Money BlockSubsidy
		{
			get;
			set;
		}
		public Money BlockReward
		{
			get;
			set;
		}
	}
}