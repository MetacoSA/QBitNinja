using NBitcoin;
using NBitcoin.DataEncoders;
using RapidBase.Controllers;
using RapidBase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase
{
    public class WhatIsIt
    {
        public WhatIsIt(MainController controller)
        {
            this.Controller = controller;
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

        public RapidBaseConfiguration Configuration
        {
            get
            {
                return Controller.Configuration;
            }
        }


        public object Find(string data)
        {
            data = data.Trim();
            var b58 = NoException(() => WhatIsBase58.GetFromBase58Data(data));
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
                var tx = NoException(() => Controller.JsonTransaction(new uint256(data)));
                if (tx != null)
                    return tx;
            }
            var b = NoException(() => Controller.JsonBlock(BlockFeature.Parse(data), true));
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

            if ((data.StartsWith("02") && data.Length / 2 == 33) || (data.StartsWith("03") && data.Length / 2 == 65))
            {
                var pubKey = NoException(() => new PubKey(data));
                if (pubKey != null)
                    return new WhatIsPublicKey(pubKey, Network);
            }

            if (data.Length == 80 * 2)
            {
                var blockHeader = NoException(() =>
                {
                    var h = new BlockHeader();
                    h.ReadWrite(Encoders.Hex.DecodeData(data));
                    return h;
                });
                if (blockHeader != null)
                    return new WhatIsBlockHeader(blockHeader);
            }
            return null;
        }

        private Script GetScriptFromText(string data)
        {
            if (!data.Contains(' '))
                return null;
            return GetScriptFromBytes(Encoders.Hex.EncodeData(new Script(data).ToBytes(true)));
        }

        private Script GetScriptFromBytes(string data)
        {
            var bytes = Encoders.Hex.DecodeData(data);
            var script = Script.FromBytesUnsafe(bytes);
            bool hasOps = false;
            var reader = script.CreateReader(false);
            foreach (var op in reader.ToEnumerable())
            {
                hasOps = true;
                if (op.IncompleteData || (op.Name == "OP_UNKNOWN" && op.PushData == null))
                    return null;
            }
            if (!hasOps)
                return null;
            return script;
        }


        private bool TryFetchRedeemOrPubKey(WhatIsAddress address)
        {
            if (address.IsP2SH)
            {
                address.RedeemScript = TryFetchRedeem(address);
                return address.RedeemScript != null;
            }
            else
            {
                address.PublicKey = TryFetchPublicKey(address);
                return address.PublicKey != null;
            }
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
            if (result == null)
                return null;
            return new WhatIsScript(result.RedeemScript, Network);
        }

        private WhatIsPublicKey TryFetchPublicKey(WhatIsAddress address)
        {
            var scriptSig = FindScriptSig(address);
            if (scriptSig == null)
                return null;
            var result = PayToPubkeyHashTemplate.Instance.ExtractScriptSigParameters(scriptSig);
            if (result == null)
                return null;
            return new WhatIsPublicKey(result.PublicKey, Network);
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
