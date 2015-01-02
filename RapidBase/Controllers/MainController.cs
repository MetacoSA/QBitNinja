using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer;
using RapidBase.ModelBinders;
using RapidBase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Web.Http;
using System.Web.Http.ModelBinding;

namespace RapidBase.Controllers
{
    public class MainController : ApiController
    {
        public MainController(
            ConcurrentChain chain,
            RapidBaseConfiguration config)
        {
            Configuration = config;
            Chain = chain;
        }
        public ConcurrentChain Chain
        {
            get;
            set;
        }

        public new RapidBaseConfiguration Configuration
        {
            get;
            set;
        }


        [HttpGet]
        [Route("transactions/{txId}")]
        public object Transaction(
            [ModelBinder(typeof(BitcoinSerializableModelBinder))]
            uint256 txId,
            DataFormat format = DataFormat.Json
            )
        {
            if (format == DataFormat.Json)
                return JsonTransaction(txId);
            else
                return RawTransaction(txId);
        }

        public GetTransactionResponse JsonTransaction(uint256 txId)
        {
            var client = Configuration.Indexer.CreateIndexerClient();
            var tx = client.GetTransaction(txId);
            if (tx == null)
                throw new HttpResponseException(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ReasonPhrase = "Transaction not found"
                });
            return new GetTransactionResponse()
            {
                TransactionId = tx.TransactionId,
                Transaction = tx.Transaction,
                IsCoinbase = tx.Transaction.IsCoinBase,
                Fees = tx.Fees,
                Block = FetchBlockInformation(tx.BlockIds),
                SpentCoins = tx.SpentCoins.Select(c => new Coin(c)).ToList()
            };
        }

        private BlockInformation FetchBlockInformation(uint256[] blockIds)
        {
            var confirmed = blockIds.Select(b => Chain.GetBlock(b)).FirstOrDefault();
            if (confirmed == null)
            {
                return null;
            }
            return new BlockInformation()
            {
                BlockId = confirmed.HashBlock,
                BlockHeader = confirmed.Header,
                Confirmations = Chain.Tip.Height - confirmed.Height + 1,
                Height = confirmed.Height,
            };
        }

        public HttpResponseMessage RawTransaction(
            uint256 txId
            )
        {
            var client = Configuration.Indexer.CreateIndexerClient();
            var tx = client.GetTransaction(txId);
            if (tx == null)
                throw new HttpResponseException(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ReasonPhrase = "Transaction not found"
                });
            return Response(tx.Transaction);
        }


        public HttpResponseMessage RawBlock(
            BlockFeature blockFeature, bool headerOnly)
        {
            var block = GetBlock(blockFeature, headerOnly);
            if (block == null)
            {
                throw new HttpResponseException(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ReasonPhrase = "Block not found"
                });
            }
            return Response(headerOnly ? (IBitcoinSerializable)block.Header : block);
        }

        [HttpGet]
        [Route("blocks/{blockFeature}")]
        public object Block(
            [ModelBinder(typeof(BlockFeatureModelBinder))]
            BlockFeature blockFeature, bool headerOnly = false, DataFormat format = DataFormat.Json)
        {
            if (format == DataFormat.Json)
                return JsonBlock(blockFeature, headerOnly);
            else
                return RawBlock(blockFeature, headerOnly);
        }

        [HttpGet]
        [Route("balances/{address}")]
        public BalanceModel Balance(
            [ModelBinder(typeof(Base58ModelBinder))]
            BitcoinAddress address)
        {
            var client = Configuration.Indexer.CreateIndexerClient();
            CancellationTokenSource cancel = new CancellationTokenSource();
            cancel.CancelAfter(30000);

            var balance =
                client
                .GetOrderedBalance(address)
                .TakeWhile(_ => !cancel.IsCancellationRequested)
                .AsBalanceSheet(Chain);
            var result = new BalanceModel(balance, Chain)
                {
                    IsComplete = !cancel.IsCancellationRequested
                };
            if (!result.IsComplete)
                result.Total = null; //Total is not correct if not complete
            return result;
        }

        [HttpGet]
        [Route("whatisit/{data}")]
        public object WhatIsIt(string data)
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
                var tx = NoException(() => JsonTransaction(new uint256(data)));
                if (tx != null)
                    return tx;
            }
            var b = NoException(() => JsonBlock(BlockFeature.Parse(data), true));
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

            return "Good question Holmes !";
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

        public Network Network
        {
            get
            {
                return Configuration.Indexer.Network;
            }
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

        private GetBlockResponse JsonBlock(BlockFeature blockFeature, bool headerOnly)
        {
            var block = GetBlock(blockFeature, headerOnly);
            if (block == null)
            {
                throw new HttpResponseException(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ReasonPhrase = "Block not found"
                });
            }
            return new GetBlockResponse()
            {
                AdditionalInformation = FetchBlockInformation(new[] { block.Header.GetHash() }) ?? new BlockInformation(block.Header),
                Block = headerOnly ? null : block
            };
        }

        private Block GetBlock(BlockFeature blockFeature, bool headerOnly)
        {
            var client = Configuration.Indexer.CreateIndexerClient();
            uint256 hash = null;
            if (blockFeature.Special != null && blockFeature.Special.Value == SpecialFeature.Last)
            {
                hash = Chain.Tip.HashBlock;
            }
            else if (blockFeature.Height != -1)
            {
                var h = Chain.GetBlock(blockFeature.Height);
                if (h == null)
                    return null;
                hash = h.HashBlock;
            }
            else
            {
                hash = blockFeature.BlockId;
            }
            return headerOnly ? GetHeader(hash, client) : client.GetBlock(hash);
        }

        private Block GetHeader(uint256 hash, IndexerClient client)
        {
            var header = Chain.GetBlock(hash);
            if (header == null)
            {
                var b = client.GetBlock(hash);
                if (b == null)
                    return null;
                return new Block(b.Header);
            }
            return new Block(header.Header);
        }

        private HttpResponseMessage Response(IBitcoinSerializable obj)
        {
            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
            result.Content = new ByteArrayContent(obj.ToBytes());
            result.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/octet-stream");
            return result;
        }
    }
}
