using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
