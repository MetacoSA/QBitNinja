using NBitcoin;
using NBitcoin.DataEncoders;
using QBitNinja.Controllers;
using QBitNinja.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QBitNinja
{
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

        public Network Network
        {
            get
            {
                return Controller.Network;
            }
        }

        public ConsensusFactory ConsensusFactory => Network.Consensus.ConsensusFactory;

        public QBitNinjaConfiguration Configuration
        {
            get
            {
                return Controller.Configuration;
            }
        }


        public async Task<object> Find(string data)
        {
            data = data.Trim();
            var b58 = NoException(() => WhatIsBase58.GetFromBitcoinString(data));
            if (b58 != null)
            {
                if (b58 is WhatIsAddress)
                {
                    var address = (WhatIsAddress)b58;
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
            var b = NoException(() => Controller.JsonBlock(BlockFeature.Parse(data), true, false));
            if (b != null)
                return b;

            if (data.Length == 0x28) //Hash of pubkey or script
            {
                TxDestination dest = new KeyId(data);
                var address = new WhatIsAddress(dest.GetAddress(Network));
                if (TryFetchRedeemOrPubKey(address))
                    return address;

                dest = new ScriptId(data);
                address = new WhatIsAddress(dest.GetAddress(Network));
                if (TryFetchRedeemOrPubKey(address))
                    return address;
            }


            var script = NoException(() => GetScriptFromBytes(data));
            if (script != null)
                return new WhatIsScript(script, Network);
            script = NoException(() => GetScriptFromText(data));
            if (script != null)
                return new WhatIsScript(script, Network);

            var sig = NoException(() => new TransactionSignature(Encoders.Hex.DecodeData(data)));
            if (sig != null)
                return new WhatIsTransactionSignature(sig);

            var pubkeyBytes = NoException(() => Encoders.Hex.DecodeData(data));
            if (pubkeyBytes != null && PubKey.Check(pubkeyBytes, true))
            {
                var pubKey = NoException(() => new PubKey(data));
                if (pubKey != null)
                    return new WhatIsPublicKey(pubKey, Network);
            }

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
            var indexer = Configuration.Indexer.CreateIndexerClient();
            var scriptSig = indexer
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
            var scriptSig = FindScriptSig(address);
            if (scriptSig == null)
                return null;
            var result = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(scriptSig);
            return result == null ? null : new WhatIsScript(result.RedeemScript, Network);
        }

        private WhatIsPublicKey TryFetchPublicKey(WhatIsAddress address)
        {
            var scriptSig = FindScriptSig(address);
            if (scriptSig == null)
                return null;
            var result = PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(scriptSig);
            return result == null ? null : new WhatIsPublicKey(result.PublicKey, Network);
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
