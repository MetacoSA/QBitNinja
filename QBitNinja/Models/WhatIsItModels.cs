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
                Address =  address.ToString();
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
            Address = new WhatIsAddress(pubkey.GetAddress(network));
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

            switch (((int)signature.SigHash & 31))
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

            R = signature.Signature.R.ToString(16);
            S = signature.Signature.S.ToString(16);
        }

        public string Raw
        {
            get;
            set;
        }
        public string R
        {
            get;
            set;
        }

        public string S
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
        public WhatIsBase58(Base58Data data)
        {
            Base58 = data.ToString();
            Type = ToString(data.Type);
            Network = data.Network;
        }

        private static string ToString(Base58Type base58Type)
        {
            return Enum.GetNames(typeof(Base58Type)).FirstOrDefault(n => ((Base58Type)Enum.Parse(typeof(Base58Type), n)) == base58Type);
        }

        public string Base58
        {
            get;
            set;
        }
        public string Type
        {
            get;
            set;
        }
        public Network Network
        {
            get;
            set;
        }

        public static WhatIsBase58 GetFromBase58Data(string data)
        {
            var b58 = Base58Data.GetFromBase58Data(data);
            if (b58 != null)
            {
                switch (b58.Type)
                {
                    case Base58Type.SCRIPT_ADDRESS:
                    case Base58Type.PUBKEY_ADDRESS:
                    case Base58Type.WITNESS_P2WPKH:
                    case Base58Type.WITNESS_P2WSH:
                        return new WhatIsAddress((BitcoinAddress)b58);
                    case Base58Type.SECRET_KEY:
                        return new WhatIsPrivateKey((BitcoinSecret)b58);
                    case Base58Type.COLORED_ADDRESS:
                        return new WhatIsColoredAddress((BitcoinColoredAddress)b58);
                    default:
                        return new WhatIsBase58(b58);
                }
            }
            throw new FormatException("Not a base58");
        }
    }
}
