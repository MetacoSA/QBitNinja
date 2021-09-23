using NBitcoin;
using NBitcoin.DataEncoders;
using QBitNinja.Controllers;
using QBitNinja.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QBitNinja
{
    /// <summary>
    /// Utility class to aid in recognizing a few kinds of objects from some string representations.
    /// </summary>
    public class WhatIsIt
    {
        public WhatIsIt(MainController controller)
        {
            Controller = controller;
        }

        public MainController Controller
        {
            get;
            set; 
        }

        public Network Network => Controller.Network;

        public ConsensusFactory ConsensusFactory => Network.Consensus.ConsensusFactory;

        public QBitNinjaConfiguration Configuration => Controller.Configuration;

        /// <summary>
        /// Try to interpret the given string in a few ways in order to detect what object it's supposed to represent.
        /// </summary>
        /// <returns>The object represented by the input string. This may be a Bitcoin address, a script, a signature, a public key, etc.</returns>
        public async Task<object> Find(string data)
        {
            data = data.Trim();


            // Is it a Bitcoin address?
            var b58 = NoException(() => WhatIsBase58.GetFromBitcoinString(data));

            if (b58 != null)
            {
                if (b58 is WhatIsAddress address)
                {
                    await TryFetchRedeemOrPubKey(address);  // Shouldn't the return value here be checked?
                }

                return b58;
            }


            // Is it a transaction ID?
            if (data.Length == 0x40)
            {
                try
                {
                    return await Controller.JsonTransaction(uint256.Parse(data), false);
                }
                catch
                {
                    // Well, apparently it's not a transaction ID.
                }
            }


            // Is it a block feature?
            var b = NoException(() => Controller.JsonBlock(BlockFeature.Parse(data), true, false));

            if (b != null)
                return b;


            // Is it the hash of a public key (modeled as KeyId in NBitcoin), or is it the hash of a script ID?
            if (data.Length == 0x28) // Hash of pubkey or script
            {
                TxDestination dest = new KeyId(data);

                var address = new WhatIsAddress(dest.GetAddress(Network));

                if (await TryFetchRedeemOrPubKey(address))
                    return address;

                dest = new ScriptId(data);
                address = new WhatIsAddress(dest.GetAddress(Network));

                if (await TryFetchRedeemOrPubKey(address))
                    return address;
            }


            // Is it a script?
            var script = NoException(() => GetScriptFromBytes(data));

            if (script != null)
                return new WhatIsScript(script, Network);

            script = NoException(() => GetScriptFromText(data));

            if (script != null)
                return new WhatIsScript(script, Network);


            // Is it a transaction signature?
            var sig = NoException(() => new TransactionSignature(Encoders.Hex.DecodeData(data)));

            if (sig != null)
                return new WhatIsTransactionSignature(sig);


            // Is it a hexstring representing the bytes of a public key?
            var pubkeyBytes = NoException(() => Encoders.Hex.DecodeData(data));

            if (pubkeyBytes != null && PubKey.Check(pubkeyBytes, true))
            {
                var pubKey = NoException(() => new PubKey(data));

                if (pubKey != null)
                    return new WhatIsPublicKey(pubKey, Network);
            }


            // Is it a blockheader?
            if (data.Length == 80 * 2)
            {
                var blockHeader = NoException(() =>
                {
                    var h = ConsensusFactory.CreateBlockHeader();
                    h.ReadWrite(Encoders.Hex.DecodeData(data), ConsensusFactory);
                    return h;
                });

                if (blockHeader != null)
                    return new WhatIsBlockHeader(blockHeader);
            }


            // No idea what this is.
            return null;
        }

        private static Script GetScriptFromText(string data)
        {
            if (!data.Contains(' '))
                return null;
            return GetScriptFromBytes(Encoders.Hex.EncodeData(new Script(data).ToBytes(true)));
        }

        private static Script GetScriptFromBytes(string data)
        {
            var bytes = Encoders.Hex.DecodeData(data);
            var script = Script.FromBytesUnsafe(bytes);
            bool hasOps = false;
            var reader = script.CreateReader();
            foreach (var op in reader.ToEnumerable())
            {
                hasOps = true;
                if (op.IsInvalid || (op.Name == "OP_UNKNOWN" && op.PushData == null))
                    return null;
            }
            return !hasOps ? null : script;
        }

        private async Task<bool> TryFetchRedeemOrPubKey(WhatIsAddress address)
        {
            if (address.IsP2SH)
            {
                address.RedeemScript = await TryFetchRedeem(address);
                return address.RedeemScript != null;
            }
            else
            {
                address.PublicKey = await TryFetchPublicKey(address);
                return address.PublicKey != null;
            }
        }

        private async Task<Script> FindScriptSig(WhatIsAddress address)
        {
            var indexer = Configuration.Indexer.CreateIndexerClient();
            var scriptSig = (await indexer
                            .GetOrderedBalance(address.ScriptPubKey.Raw))
                            .Where(b => b.SpentCoins.Count != 0)
                            .Select(b => new
                            {
                                SpentN = b.SpentIndices[0],
                                Tx = indexer.GetTransaction(b.TransactionId)
                            })
                            .Where(o => o.Tx != null)
                            .Select(o => o.Tx.Transaction.Inputs[o.SpentN].ScriptSig)
                            .FirstOrDefault();
            return scriptSig;
        }

        private async Task<WhatIsScript> TryFetchRedeem(WhatIsAddress address)
        {
            var scriptSig = await FindScriptSig(address);
            if (scriptSig == null)
                return null;
            var result = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(scriptSig);
            return result == null ? null : new WhatIsScript(result.RedeemScript, Network);
        }

        private async Task<WhatIsPublicKey> TryFetchPublicKey(WhatIsAddress address)
        {
            var scriptSig = await FindScriptSig(address);
            if (scriptSig == null)
                return null;
            var result = PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(scriptSig);
            return result == null ? null : new WhatIsPublicKey(result.PublicKey, Network);
        }

        private T NoException<T>(Func<T> act) where T : class
        {
            try
            {
                return act();
            }
            catch
            {
                return null;
            }
        }
    }
}
