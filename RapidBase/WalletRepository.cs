using NBitcoin.Crypto;
using NBitcoin.Indexer;
using RapidBase.Models;
using System;
using System.Text;

namespace RapidBase
{
    public class WalletRepository
    {
        public WalletRepository(IndexerClient indexer,
                                CrudTableFactory tableFactory)
        {
            if (indexer == null)
                throw new ArgumentNullException("indexer");
            if (tableFactory == null)
                throw new ArgumentNullException("tableFactory");
            _walletAddressesTable = tableFactory.GetTable<WalletAddress>("wa");
            _walletTable = tableFactory.GetTable<WalletModel>("wm");
            _indexer = indexer;
        }

        private readonly CrudTable<WalletAddress> _walletAddressesTable;
        public CrudTable<WalletAddress> WalletAddressesTable
        {
            get
            {
                return _walletAddressesTable;
            }
        }

        private readonly CrudTable<WalletModel> _walletTable;
        public CrudTable<WalletModel> WalletTable
        {
            get
            {
                return _walletTable;
            }
        }

        private readonly IndexerClient _indexer;
        public IndexerClient Indexer
        {
            get
            {
                return _indexer;
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

        public ScriptRule AddAddress(string walletName, WalletAddress address)
        {
            if (address.Address == null)
                throw new ArgumentException("Address should not be null", "address.Address");
            var rule = new ScriptRule
            {
                CustomData = address.CustomData == null ? null : address.CustomData.ToString(),
                ScriptPubKey = address.Address.ScriptPubKey,
                RedeemScript = address.RedeemScript
            };
            Indexer.AddWalletRule(walletName, rule);
            WalletAddressesTable.Create(walletName.ToLowerInvariant(), Hash(address), address);
            return rule;
        }

        private static string Hash(WalletAddress address)
        {
            return Hashes.Hash256(Encoding.UTF8.GetBytes(Serializer.ToString(address))).ToString();
        }

        public WalletAddress[] GetAddresses(string walletName)
        {
            return WalletAddressesTable.Read(walletName.ToLowerInvariant());
        }


        //public void AddKeySet(KeySet keyset)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
