using NBitcoin;
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
	public class WalletName
	{
		public WalletName(string name)
		{
			if(name == null)
				throw new ArgumentNullException("name");
			_Name = name;
		}
		string _Name;
		public override string ToString()
		{
			return _Name.ToString();
		}
	}
	public class BalanceSelector
	{
		string _Str;

		public BalanceSelector(string selector)
		{
			if(selector == null)
				throw new ArgumentNullException("selector");
			_Str = selector;
		}

		public BalanceSelector(WalletName walletName)
		{
			if(walletName == null)
				throw new ArgumentNullException("walletName");
			_Str = "W-" + walletName;
		}

		public BalanceSelector(Script script)
		{
			if(script == null)
				throw new ArgumentNullException("script");
			_Str = "0x" + script.ToHex();
		}

		public BalanceSelector(IDestination destination)
		{
			if(destination == null)
				throw new ArgumentNullException("destination");
			if(destination is BitcoinAddress || destination is BitcoinColoredAddress)
			{
				_Str = destination.ToString();
			}

			if(_Str == null)
			{
				_Str = "0x" + destination.ScriptPubKey.ToHex();
			}
		}

		public override string ToString()
		{
			return _Str;
		}
	}
}
