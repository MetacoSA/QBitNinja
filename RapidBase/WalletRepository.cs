using NBitcoin.Crypto;
using NBitcoin.Indexer;
using RapidBase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase
{
    public class WalletRepository
    {
        public WalletRepository(IndexerClient indexer,
                                CrudTable<WalletModel> walletTable,
                                CrudTable<WalletAddress> addressTable)
        {
            if (indexer == null)
                throw new ArgumentNullException("indexer");
            if (walletTable == null)
                throw new ArgumentNullException("table");
            if (addressTable == null)
                throw new ArgumentNullException("addressTable");
            _WalletAddressesTable = addressTable;
            _WalletTable = walletTable;
            _Indexer = indexer;
        }

        private readonly CrudTable<WalletAddress> _WalletAddressesTable;
        public CrudTable<WalletAddress> WalletAddressesTable
        {
            get
            {
                return _WalletAddressesTable;
            }
        }

        private readonly CrudTable<WalletModel> _WalletTable;
        public CrudTable<WalletModel> WalletTable
        {
            get
            {
                return _WalletTable;
            }
        }

        private readonly IndexerClient _Indexer;
        public IndexerClient Indexer
        {
            get
            {
                return _Indexer;
            }
        }

        public void Create(WalletModel wallet)
        {
            WalletTable.Create("w", wallet.Name.ToLowerInvariant(), wallet);
        }

        public WalletModel[] Get()
        {
            return WalletTable.Read("w");
        }

        public void AddAddress(string walletName, WalletAddress address)
        {
            if (address.Address == null)
                throw new ArgumentException("Address should not be null", "address.Address");
            Indexer.AddWalletRule(walletName, new ScriptRule()
            {
                CustomData = address.CustomData == null ? null : address.CustomData.ToString(),
                ScriptPubKey = address.Address.ScriptPubKey,
                RedeemScript = address.RedeemScript
            });
            WalletAddressesTable.Create(walletName.ToLowerInvariant(), Hash(address), address);
        }

        private string Hash(WalletAddress address)
        {
            return Hashes.Hash256(Encoding.UTF8.GetBytes(Serializer.ToString(address))).ToString();
        }

        public WalletAddress[] GetAddresses(string walletName)
        {
            return WalletAddressesTable.Read(walletName.ToLowerInvariant());
        }

    }
}
