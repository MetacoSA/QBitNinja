using NBitcoin;
using NBitcoin.DataEncoders;
using QBitNinja.Controllers;
using QBitNinja.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin.Indexer;

namespace QBitNinja
{
    public class WhatIsIt
    {
        public WhatIsIt(MainController controller)
        {
            Controller = controller;
        }

        public MainController Controller { get; set; }

        public Network Network => Controller.Network;

        public ConsensusFactory ConsensusFactory => Network.Consensus.ConsensusFactory;

        public QBitNinjaConfiguration Configuration => Controller.Configuration;

        public async Task<object> Find(string data)
        {
            data = data.Trim();
            WhatIsBase58 b58 = NoException(() => WhatIsBase58.GetFromBitcoinString(data));
            if (b58 != null)
            {
                if (b58 is WhatIsAddress address)
                {
                    TryFetchRedeemOrPubKey(address);
                }

                return b58;
            }

            if (data.Length == 0x40)
            {
                try
                {
                    return await Controller.JsonTransaction(uint256.Parse(data), false);
                }
                catch
                {
                }
            }

            GetBlockResponse b = NoException(() => Controller.JsonBlock(BlockFeature.Parse(data), true, false));
            if (b != null)
            {
                return b;
            }

            if (data.Length == 0x28) //Hash of pubkey or script
            {
                TxDestination dest = new KeyId(data);
                WhatIsAddress address = new WhatIsAddress(dest.GetAddress(Network));
                if (TryFetchRedeemOrPubKey(address))
                {
                    return address;
                }

                dest = new ScriptId(data);
                address = new WhatIsAddress(dest.GetAddress(Network));
                if (TryFetchRedeemOrPubKey(address))
                {
                    return address;
                }
            }


            Script script = NoException(() => GetScriptFromBytes(data));
            if (script != null)
            {
                return new WhatIsScript(script, Network);
            }

            script = NoException(() => GetScriptFromText(data));
            if (script != null)
            {
                return new WhatIsScript(script, Network);
            }

            TransactionSignature sig = NoException(() => new TransactionSignature(Encoders.Hex.DecodeData(data)));
            if (sig != null)
            {
                return new WhatIsTransactionSignature(sig);
            }

            byte[] pubkeyBytes = NoException(() => Encoders.Hex.DecodeData(data));
            if (pubkeyBytes != null && PubKey.Check(pubkeyBytes, true))
            {
                PubKey pubKey = NoException(() => new PubKey(data));
                if (pubKey != null)
                {
                    return new WhatIsPublicKey(pubKey, Network);
                }
            }

            if (data.Length == 80 * 2)
            {
                BlockHeader blockHeader = NoException(() =>
                {
                    BlockHeader h = ConsensusFactory.CreateBlockHeader();
                    h.ReadWrite(Encoders.Hex.DecodeData(data), ConsensusFactory);
                    return h;
                });

                if (blockHeader != null)
                {
                    return new WhatIsBlockHeader(blockHeader);
                }
            }

            return null;
        }

        private static Script GetScriptFromText(string data)
        {
            return data.Contains(' ')
                       ? GetScriptFromBytes(Encoders.Hex.EncodeData(new Script(data).ToBytes(true)))
                       : null;
        }

        private static Script GetScriptFromBytes(string data)
        {
            byte[] bytes = Encoders.Hex.DecodeData(data);
            Script script = Script.FromBytesUnsafe(bytes);
            var hasOps = false;
            ScriptReader reader = script.CreateReader();
            foreach (Op op in reader.ToEnumerable())
            {
                hasOps = true;
                if (op.IsInvalid
                    || op.Name == "OP_UNKNOWN"
                    && op.PushData == null)
                {
                    return null;
                }
            }

            return hasOps ? script : null;
        }


        private bool TryFetchRedeemOrPubKey(WhatIsAddress address)
        {
            if (address.IsP2SH)
            {
                address.RedeemScript = TryFetchRedeem(address);
                return address.RedeemScript != null;
            }

            address.PublicKey = TryFetchPublicKey(address);
            return address.PublicKey != null;
        }


        private Script FindScriptSig(WhatIsAddress address)
        {
            IndexerClient indexer = Configuration.Indexer.CreateIndexerClient();
            Script scriptSig = indexer
                            .GetOrderedBalance(address.ScriptPubKey.Raw)
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

        private WhatIsScript TryFetchRedeem(WhatIsAddress address)
        {
            Script scriptSig = FindScriptSig(address);
            if (scriptSig == null)
            {
                return null;
            }

            PayToScriptHashSigParameters result = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(scriptSig);
            return result != null
                       ? new WhatIsScript(result.RedeemScript, Network)
                       : null;
        }

        private WhatIsPublicKey TryFetchPublicKey(WhatIsAddress address)
        {
            Script scriptSig = FindScriptSig(address);
            if (scriptSig == null)
            {
                return null;
            }

            PayToPubkeyHashScriptSigParameters result = PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(scriptSig);
            return result != null
                       ? new WhatIsPublicKey(result.PublicKey, Network)
                       : null;
        }

        public T NoException<T>(Func<T> act) where T : class
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
