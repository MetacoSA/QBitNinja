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

        public ChainedBlock GetChainedBlock(ChainBase chain)
        {
            ChainedBlock chainedBlock;
            if (Special != null && Special.Value == SpecialFeature.Last)
            {
                chainedBlock = chain.Tip;
            }
            else if (Height != -1)
            {
                var h = chain.GetBlock(Height);
                if (h == null)
                    return null;
                chainedBlock = h;
            }
            else
            {
                chainedBlock = chain.GetBlock(BlockId);
            }
            if (chainedBlock != null)
            {
                var height = chainedBlock.Height + Offset;
                height = Math.Max(0, height);
                chainedBlock = chain.GetBlock(height);
            }
            return chainedBlock;
        }

        public static BlockFeature Parse(string str)
        {
            var input = str;
            var feature = new BlockFeature();
            uint height;

            var split = str.Split(new[] { '-', '+' }, StringSplitOptions.None);
            if (split.Length != 1 && split.Length != 2)
                ThrowInvalidFormat();
            str = split[0];
            if(split.Length == 2)
            {
                var offset = TryParse(split[1]);
                feature.Offset = input.Contains("-") ? -offset : offset;
            }


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

            ThrowInvalidFormat();
            return null;
        }

        private static void ThrowInvalidFormat()
        {
            throw new FormatException("Invalid block feature, expecting block height or hash");
        }

        private static int TryParse(string str)
        {
            int result;
            if (!int.TryParse(str, out result))
                ThrowInvalidFormat();
            return result;
        }

        public int Offset
        {
            get;
            set;
        }
    }
}
