using NBitcoin;
using System.Collections.Generic;

namespace QBitNinja.Models
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

        public List<Coin> SpentCoins
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
