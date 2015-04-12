using NBitcoin;

namespace QBitNinja.Models
{
    public class GetBlockResponse
    {
        public BlockInformation AdditionalInformation
        {
            get;
            set;
        }
        public Block Block
        {
            get;
            set;
        }
    }
}
