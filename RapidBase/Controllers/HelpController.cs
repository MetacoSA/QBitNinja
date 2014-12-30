using RapidBase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Http.Routing;
using System.Web.Mvc;

namespace RapidBase.Controllers
{
    public class HelpController : Controller
    {
        public ActionResult Index()
        {
            return View(new HelpModel()
            {
                Routes = new[] 
                { 
                    new RouteModel()
                    {
                        Template = "blocks/[blockId|height|tip]?format=[json|raw]&headeronly=[false|true]",
                        Samples = new[]
                        {
                            "blocks/0000000000000000119fe3f65fd3038cbe8429ad2cf7c2de1e5e7481b34a01b4",
                            "blocks/321211",
                            "blocks/tip",
                            "blocks/tip?format=json",
                            "blocks/tip?format=json&headeronly=true",
                            "blocks/tip?format=raw",
                            "blocks/tip?format=raw&headeronly=true",
                        }
                    },
                    new RouteModel()
                    {
                        Template = "transactions/[txId]?format=[json|raw]&headeronly=[false|true]",
                        Samples = new[]
                        {
                            "transactions/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a",
                            "transactions/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a?format=json",
                            "transactions/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a?format=json&headeronly=true",
                            "transactions/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a?format=raw",
                            "transactions/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a?format=raw&headeronly=true",
                        }
                    },
                    new RouteModel()
                    {
                        Template = "whatisit/[address|txId|blockId|blockheader|base58|transaction|script|scriptbytes]",
                        Samples = new[]
                        {
                            "whatisit/15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe",
                            "whatisit/02012edcbdf6f8b7d4d315338423196ce1c4251ede5a8c9c1dfff645f67a008935",
                            "whatisit/356facdac5f5bcae995d13e667bb5864fd1e7d59",

                            "whatisit/3P2sV4w1ZSk5gr6eePd6U2V56Mx5fT3RkD",
                            "whatisit/ea1bea7de1b975b962adbd57a9e0533449962a80",

                            "whatisit/d3294938d12105a27a55af9c02864d6f633741bd5c5340a18972935a457275b9",

                            "whatisit/0000000000000000080feeba134552a002aeb08e815aad1c41108f680d7b5f58",
                            "whatisit/335598"
                        }
                    }
                }
            });
        }
    }
}
