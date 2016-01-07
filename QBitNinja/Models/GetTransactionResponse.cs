using NBitcoin;
using System;
using System.Collections.Generic;

#if !CLIENT
namespace QBitNinja.Models
#else
namespace QBitNinja.Client.Models
#endif
{
    public class BlockInformation
    {
        public BlockInformation()
        {

        }
        public BlockInformation(BlockHeader header)
        {
            BlockId = header.GetHash();
            BlockHeader = header;
            Height = -1;
            Confirmations = -1;
        }
        public uint256 BlockId
        {
            get;
            set;
        }

        public BlockHeader BlockHeader
        {
            get;
            set;
        }
        public int Height
        {
            get;
            set;
        }
        public int Confirmations
        {
            get;
            set;
        }
    }
    public class GetTransactionResponse
    {
        public GetTransactionResponse()
        {
            ReceivedCoins = new List<ICoin>();
            SpentCoins = new List<ICoin>();
        }
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

        public bool IsCoinbase
        {
            get;
            set;
        }

        public BlockInformation Block
        {
            get;
            set;
        }

        public List<ICoin> SpentCoins
        {
            get;
            set;
        }

        public List<ICoin> ReceivedCoins
        {
            get;
            set;
        }

        public DateTimeOffset FirstSeen
        {
            get;
            set;
        }

        public Money Fees
        {
            get;
            set;
        }
    }
}
