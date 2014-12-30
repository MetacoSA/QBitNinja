using NBitcoin;
using RapidBase.ModelBinders;
using RapidBase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
        public GetTransactionResponse Transaction(
            [ModelBinder(typeof(BitcoinSerializableModelBinder))]
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
            return new GetTransactionResponse()
            {
                TransactionId = tx.TransactionId,
                Transaction = tx.Transaction,
                Fees = tx.Fees,
                Block = FetchBlockInformation(tx.BlockIds),
                SpentCoins = tx.SpentCoins.Select(c => new Coin(c)).ToList()
            };
        }

        private BlockInformation FetchBlockInformation(uint256[] blockIds)
        {
            var confirmed = blockIds.Select(b => Chain.GetBlock(b)).FirstOrDefault();
            if (confirmed == null)
                return null;
            return new BlockInformation()
            {
                BlockId = confirmed.HashBlock,
                BlockHeader = confirmed.Header,
                Confirmations = Chain.Tip.Height - confirmed.Height + 1,
                Height = confirmed.Height,
            };
        }

        [HttpGet]
        [Route("rawtransactions/{txId}")]
        public HttpResponseMessage RawTransaction(
            [ModelBinder(typeof(BitcoinSerializableModelBinder))]
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

        [HttpGet]
        [Route("rawblocks/{blockFeature}")]
        public HttpResponseMessage RawBlock(
            [ModelBinder(typeof(BlockFeatureModelBinder))]
            BlockFeature blockFeature)
        {
            var block = GetBlock(blockFeature);
            if (block == null)
            {
                throw new HttpResponseException(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ReasonPhrase = "Block not found"
                });
            }
            return Response(block);
        }

        [HttpGet]
        [Route("blocks/{blockFeature}")]
        public GetBlockResponse Block(
            [ModelBinder(typeof(BlockFeatureModelBinder))]
            BlockFeature blockFeature)
        {
            var block = GetBlock(blockFeature);
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
                AdditionalInformation = FetchBlockInformation(new[] { block.Header.GetHash() }),
                Block = block
            };
        }

        private Block GetBlock(BlockFeature blockFeature)
        {
            var client = Configuration.Indexer.CreateIndexerClient();
            if (blockFeature.Height != -1)
            {
                var h = Chain.GetBlock(blockFeature.Height);
                if (h == null)
                    return null;
                return client.GetBlock(h.HashBlock);
            }
            else
            {
                return client.GetBlock(blockFeature.BlockId);
            }
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
