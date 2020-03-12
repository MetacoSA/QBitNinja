using System.Globalization;
using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Linq;

#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
	public class WhatIsScript
	{
		public WhatIsScript()
		{

		}
		public WhatIsScript(Script script, Network network)
		{
			Raw = script;
			Asm = script.ToString();
			Hash160 = new uint160(script.Hash.ToBytes(false), false);
			Hash256 = new uint256(script.WitHash.ToBytes(false), false);
			var address = script.GetDestinationAddress(network);
			if(address != null)
				Address = address.ToString();
		}

		public uint160 Hash160
		{
			get;
			set;
		}

		public uint256 Hash256
		{
			get;
			set;
		}

		public string Address
		{
			get;
			set;
		}

		public Script Raw
		{
			get;
			set;
		}

		public string Asm
		{
			get;
			set;
		}
	}

	public class WhatIsPublicKey
	{
		public WhatIsPublicKey()
		{

		}
		public WhatIsPublicKey(PubKey pubkey, Network network)
		{
			Hex = pubkey.ToHex();
			Address = new WhatIsAddress(pubkey.GetAddress(ScriptPubKeyType.Legacy, network));
			P2SHAddress = new WhatIsAddress(pubkey.ScriptPubKey.GetScriptAddress(network))
			{
				RedeemScript = new WhatIsScript(pubkey.ScriptPubKey, network)
			};
			ScriptPubKey = new WhatIsScript(pubkey.ScriptPubKey, network);
			IsCompressed = pubkey.IsCompressed;
		}

		public string Hex
		{
			get;
			set;
		}

		public bool IsCompressed
		{
			get;
			set;
		}

		public WhatIsAddress Address
		{
			get;
			set;
		}
		[Newtonsoft.Json.JsonProperty(PropertyName = "p2shAddress")]
		public WhatIsAddress P2SHAddress
		{
			get;
			set;
		}

		public WhatIsScript ScriptPubKey
		{
			get;
			set;
		}
	}

	public class WhatIsColoredAddress : WhatIsBase58
	{
		public WhatIsColoredAddress()
		{

		}

		public WhatIsColoredAddress(BitcoinColoredAddress colored)
			: base(colored)
		{
			UncoloredAddress = colored.Address;
		}

		public BitcoinAddress UncoloredAddress
		{
			get;
			set;
		}
	}

	public class WhatIsPrivateKey : WhatIsBase58
	{
		public WhatIsPrivateKey()
		{

		}
		public WhatIsPrivateKey(BitcoinSecret secret)
			: base(secret)
		{
			PublicKey = new WhatIsPublicKey(secret.PrivateKey.PubKey, secret.Network);
		}

		public WhatIsPublicKey PublicKey
		{
			get;
			set;
		}
	}

	public class WhatIsBlockHeader
	{
		public WhatIsBlockHeader(BlockHeader blockHeader)
		{
			Hash = blockHeader.GetHash();
			Previous = blockHeader.HashPrevBlock;
			Time = blockHeader.BlockTime;
			Nonce = blockHeader.Nonce;
			HashMerkelRoot = blockHeader.HashMerkleRoot;
			Version = blockHeader.Version.ToString(CultureInfo.InvariantCulture);
			Bits = blockHeader.Bits.ToString();
			Difficulty = blockHeader.Bits.Difficulty;
		}

		public string Version
		{
			get;
			set;
		}

		public uint256 Hash
		{
			get;
			set;
		}

		public uint256 Previous
		{
			get;
			set;
		}

		public DateTimeOffset Time
		{
			get;
			set;
		}

		public uint Nonce
		{
			get;
			set;
		}

		public uint256 HashMerkelRoot
		{
			get;
			set;
		}

		public string Bits
		{
			get;
			set;
		}

		public double Difficulty
		{
			get;
			set;
		}
	}

	public class WhatIsTransactionSignature
	{
		public WhatIsTransactionSignature()
		{

		}
		public WhatIsTransactionSignature(TransactionSignature signature)
		{
			Raw = Encoders.Hex.EncodeData(signature.ToBytes());
			AnyoneCanPay = ((int)signature.SigHash & (int)NBitcoin.SigHash.AnyoneCanPay) != 0;

			switch(((int)signature.SigHash & 31))
			{
				case (int)NBitcoin.SigHash.Single:
					SigHash = "Single";
					break;
				case (int)NBitcoin.SigHash.None:
					SigHash = "None";
					break;
				default:
					SigHash = "All";
					break;
			}
		}

		public string Raw
		{
			get;
			set;
		}

		public bool AnyoneCanPay
		{
			get;
			set;
		}
		public string SigHash
		{
			get;
			set;
		}
	}

	public class WhatIsExtPubKey : WhatIsBase58
	{
		public WhatIsExtPubKey(BitcoinExtPubKey data) : base(data)
		{
			PubKey = data.ExtPubKey.PubKey.ToString();
			Hardened = data.ExtPubKey.IsHardened;
			ChainCode = Encoders.Hex.EncodeData(data.ExtPubKey.ChainCode);
			Child = data.ExtPubKey.Child;
			Depth = data.ExtPubKey.Depth;
			Fingerprint = data.ExtPubKey.ParentFingerprint.ToString();
		}

		public string PubKey
		{
			get; set;
		}

		public bool Hardened
		{
			get; set;
		}
		public string ChainCode
		{
			get;
			set;
		}
		public uint Child
		{
			get;
			set;
		}
		public byte Depth
		{
			get;
			set;
		}
		public string Fingerprint
		{
			get;
			set;
		}
	}

	public class WhatIsExtKey : WhatIsBase58
	{
		public WhatIsExtKey(BitcoinExtKey data) : base(data)
		{
			PrivateKey = Encoders.Hex.EncodeData(data.PrivateKey.ToBytes());
			ExtPubKey = data.Neuter().ToString();
		}

		public string ExtPubKey
		{
			get; set;
		}
		public string PrivateKey
		{
			get; set;
		}
	}


	public class WhatIsAddress : WhatIsBase58
	{
		public WhatIsAddress()
		{

		}
		public WhatIsAddress(BitcoinAddress address)
			: base(address)
		{
			IsP2SH = address is BitcoinScriptAddress;
			ScriptPubKey = new WhatIsScript(address.ScriptPubKey, address.Network);
			Hash = GetHash(address);
			
			if(address is IBase58Data)
				ColoredAddress = address.ToColoredAddress().ToString();
		}

		private string GetHash(BitcoinAddress address)
		{
			BitcoinPubKeyAddress pubkey = address as BitcoinPubKeyAddress;
			if(pubkey != null)
				return pubkey.Hash.ToString();

			BitcoinScriptAddress script = address as BitcoinScriptAddress;
			if(script != null)
				return script.Hash.ToString();

			BitcoinWitPubKeyAddress wit1 = address as BitcoinWitPubKeyAddress;
			if(wit1 != null)
				return wit1.Hash.ToString();

			BitcoinWitScriptAddress wit2 = address as BitcoinWitScriptAddress;
			if(wit2 != null)
				return wit2.Hash.ToString();
			return null;
		}

		public bool IsP2SH
		{
			get;
			set;
		}

		public string Hash
		{
			get;
			set;
		}
		public string ColoredAddress
		{
			get;
			set;
		}

		public WhatIsScript ScriptPubKey
		{
			get;
			set;
		}
		public WhatIsScript RedeemScript
		{
			get;
			set;
		}
		public WhatIsPublicKey PublicKey
		{
			get;
			set;
		}
	}
	public class WhatIsBase58
	{
		public WhatIsBase58()
		{

		}
		public WhatIsBase58(IBitcoinString data)
		{
			Data = data.ToString();
			var b58 = data as IBase58Data;
			if(b58 != null)
			{
				Type = ToString(b58.Type);
				StringType = "Base58";
			}
			var b32 = data as IBech32Data;
			if(b32 != null)
			{
				Type = ToString(b32.Type);
				StringType = "Bech32";
			}
			Network = data.Network;
		}

		private static string ToString<T>(T base58Type)
		{
			return Enum.GetNames(typeof(T)).FirstOrDefault(n => ((T)Enum.Parse(typeof(T), n)).Equals(base58Type));
		}

		public string Data
		{
			get;
			set;
		}
		public string Type
		{
			get;
			set;
		}
		public string StringType
		{
			get; set;
		}
		public Network Network
		{
			get;
			set;
		}

		public static WhatIsBase58 GetFromBitcoinString(string data)
		{
			try
			{
				var b58 = Network.Parse<IBase58Data>(data, null);
				if(b58 != null)
				{
					switch(b58.Type)
					{
						case Base58Type.SCRIPT_ADDRESS:
						case Base58Type.PUBKEY_ADDRESS:
							return new WhatIsAddress((BitcoinAddress)b58);
						case Base58Type.SECRET_KEY:
							return new WhatIsPrivateKey((BitcoinSecret)b58);
						case Base58Type.COLORED_ADDRESS:
							return new WhatIsColoredAddress((BitcoinColoredAddress)b58);
						case Base58Type.EXT_SECRET_KEY:
							return new WhatIsExtKey((BitcoinExtKey)b58);
						case Base58Type.EXT_PUBLIC_KEY:
							return new WhatIsExtPubKey((BitcoinExtPubKey)b58);
						default:
							return new WhatIsBase58(b58);
					}
				}
			}
			catch(FormatException) { }

			try
			{
				var b32 = Network.Parse<IBech32Data>(data, null);
				if(b32 != null)
				{
					switch(b32.Type)
					{
						case Bech32Type.WITNESS_PUBKEY_ADDRESS:
						case Bech32Type.WITNESS_SCRIPT_ADDRESS:
							return new WhatIsAddress((BitcoinAddress)b32);
						default:
							throw new FormatException("Invalid bech32 string");
					}

				}
			}
			catch(FormatException) { }
			throw new FormatException("Not a base58 or bech32");
		}
	}
}
