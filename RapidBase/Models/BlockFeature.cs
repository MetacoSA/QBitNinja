using NBitcoin;
using NBitcoin.DataEncoders;
using System;
using System.Linq;

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

        public static BlockFeature Parse(string str)
        {
            var feature = new BlockFeature();
            uint height;

            if (str.Equals("last", StringComparison.OrdinalIgnoreCase) || str.Equals("tip", StringComparison.OrdinalIgnoreCase))
            {
                feature.Special = SpecialFeature.Last;
                return feature;
            }

            if (uint.TryParse(str, out height))
            {
                feature.Height = (int)height;
                return feature;
            }

            if (str.Length == 0x40 && str.All(c => HexEncoder.IsDigit(c) != -1))
            {
                feature.BlockId = new uint256(str);
                return feature;
            }

            throw new FormatException("Invalid block feature, expecting block height or hash");
        }
    }
}
