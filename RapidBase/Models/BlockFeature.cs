using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase.Models
{
    public enum SpecialFeature
    {
        Last
    }
    public class BlockFeature
    {
        public BlockFeature()
        {
            Height = -1;
        }
        public int Height
        {
            get;
            set;
        }
        public uint256 BlockId
        {
            get;
            set;
        }
        public SpecialFeature? Special
        {
            get;
            set;
        }
    }
}
