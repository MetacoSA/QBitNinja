using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase.Models
{
    public class GetTransactionResponse
    {
        public Transaction Transaction
        {
            get;
            set;
        }

        public uint256 TransactionId
        {
            get;
            set;
        }
    }
}
