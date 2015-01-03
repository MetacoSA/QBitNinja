using NBitcoin;

namespace RapidBase.Models
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
