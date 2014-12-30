using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase.Models
{
    public class WhatIsScript
    {
        public WhatIsScript()
        {

        }
        public WhatIsScript(Script script)
        {
            Raw = script;
            Asm = script.ToString();
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
            P2SHAddress = new WhatIsAddress(pubkey.ScriptPubKey.GetScriptAddress(network));
            P2SHAddress.RedeemScript = new WhatIsScript(pubkey.ScriptPubKey);
            ScriptPubKey = new WhatIsScript(pubkey.ScriptPubKey);
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

    public class WhatIsPrivateKey : WhatIsBase58
    {
        public WhatIsPrivateKey()
        {

        }
        public WhatIsPrivateKey(BitcoinSecret secret)
            : base(secret)
        {
            PublicKey = new WhatIsPublicKey(secret.Key.PubKey, secret.Network);
        }

        public WhatIsPublicKey PublicKey
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
            ScriptPubKey = new WhatIsScript(address.ScriptPubKey);
            Hash = new uint160(address.Hash.ToBytes(true), false);
        }

        public bool IsP2SH
        {
            get;
            set;
        }

        public uint160 Hash
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

        private string ToString(Base58Type base58Type)
        {
            return Enum.GetNames(typeof(Base58Type))
                .Where(n => ((Base58Type)Enum.Parse(typeof(Base58Type), n)) == base58Type)
                .FirstOrDefault();
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
                        return new WhatIsAddress((BitcoinAddress)b58);
                    case Base58Type.SECRET_KEY:
                        return new WhatIsPrivateKey((BitcoinSecret)b58);
                    default:
                        return new WhatIsBase58(b58);
                }
            }
            throw new FormatException("Not a base58");
        }
    }
}
