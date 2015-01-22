using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase.Models
{
    public class KeySetData
    {
        public HDKeySet KeySet
        {
            get;
            set;
        }
        public HDKeyState State
        {
            get;
            set;
        }
    }

    public class HDKeyData
    {
        public KeyPath Path
        {
            get;
            set;
        }
        public BitcoinAddress Address
        {
            get;
            set;
        }
        public BitcoinExtPubKey ExtPubKey
        {
            get;
            set;
        }
    }

    public class HDKeyState
    {
        public KeyPath CurrentPath
        {
            get;
            set;
        }
    }
    public class HDKeySet
    {
        public string Name
        {
            get;
            set;
        }
        public BitcoinExtPubKey ExtPubKey
        {
            get;
            set;
        }

        public KeyPath Path
        {
            get;
            set;
        }
    }
}
