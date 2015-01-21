using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase.Models
{
    public class KeySet
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
