using NBitcoin;
using RapidBase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace RapidBase.Controllers
{

    public class MainController : ApiController
    {
        [Route("transactions/{txId}")]
        public GetTransactionResponse Transaction(uint256 txId)
        {
            return null;
        }
    }
}
