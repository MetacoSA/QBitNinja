using RapidBase.Models;
using System.Web.Mvc;

namespace RapidBase.Controllers
{
    public class HelpController : Controller
    {
        public ActionResult Index()
        {
            return View(new HelpModel
            {
                Routes = new[] 
                { 
                    new RouteModel
                    {
                        Template = "blocks/[blockId|height|tip]?format=[json|raw]&headeronly=[false|true]",
                        Samples = new RouteSample[]
                        {
                            "blocks/0000000000000000119fe3f65fd3038cbe8429ad2cf7c2de1e5e7481b34a01b4",
                            "blocks/321211",
                            "blocks/tip",
                            "blocks/tip-1",
                            "blocks/tip?format=json",
                            "blocks/tip?format=json&headeronly=true",
                            "blocks/tip?format=raw",
                            "blocks/tip?format=raw&headeronly=true",
                        }
                    },
                     new RouteModel
                    {
                        Template = "blocks/[blockFeature]/header",
                        Samples = new RouteSample[]
                        {
                            "blocks/tip/header"
                        }
                    },
                    new RouteModel
                    {
                        Template = "transactions/[txId]?format=[json|raw]&headeronly=[false|true]",
                        Samples = new RouteSample[]
                        {
                            "transactions/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a",
                            "transactions/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a?format=json",
                            "transactions/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a?format=json&headeronly=true",
                            "transactions/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a?format=raw",
                            "transactions/38d4cfeb57d6685753b7a3b3534c3cb576c34ca7344cd4582f9613ebf0c2b02a?format=raw&headeronly=true",
                        }
                    },
                    new RouteModel
                    {
                        Template = "balances/[address]?unspentOnly=[false|true]&from=[blockFeature]&to=[blockFeature]&continuation=[continuation]",
                        Samples = new RouteSample[]
                        {
                            "balances/15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe",
                            "balances/15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe?unspentonly=true",
                            "balances/1dice8EMZmqKvrGE4Qc9bUFf9PX3xaYDp?from=336410&until=336000",
                            "balances/1dice8EMZmqKvrGE4Qc9bUFf9PX3xaYDp?from=tip&until=336000"
                        }
                    },
                     new RouteModel
                    {
                        Template = "coloredbalances/[coloredaddress]?unspentOnly=[false|true]&from=[blockFeature]&to=[blockFeature]&continuation=[continuation]",
                        Samples = new RouteSample[]
                        {
                            "coloredbalances/akEBcY5k1dn2yeEdFnTMwdhVbHxtgHb6GGi",
                            "coloredbalances/akEBcY5k1dn2yeEdFnTMwdhVbHxtgHb6GGi?unspentonly=true",
                            "coloredbalances/akEBcY5k1dn2yeEdFnTMwdhVbHxtgHb6GGi?from=336410&until=336000",
                            "coloredbalances/akEBcY5k1dn2yeEdFnTMwdhVbHxtgHb6GGi?from=tip&until=336000"
                        }
                    },
                    new RouteModel
                    {
                        Template = "balances/[address]/summary?at=[blockFeature]",
                        Samples = new RouteSample[]
                        {
                            "balances/15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe/summary",
                            "balances/15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe/summary?at=318331",
                        }
                    },
                     new RouteModel
                    {
                        Template = "coloredbalances/[address]/summary?at=[blockFeature]",
                        Samples = new RouteSample[]
                        {
                            "coloredbalances/akEBcY5k1dn2yeEdFnTMwdhVbHxtgHb6GGi/summary",
                            "coloredbalances/akEBcY5k1dn2yeEdFnTMwdhVbHxtgHb6GGi/summary?at=318331",
                        }
                    },
                    new RouteModel
                    {
                        Template = "whatisit/[address|txId|blockId|blockheader|base58|transaction|signature|script|scriptbytes]",
                        Samples = new[]
                        {
                            new RouteSample("whatisit/15sYbVpRh6dyWycZMwPdxJWD4xbfxReeHe", "P2PKH Address"),
                            new RouteSample("whatisit/02012edcbdf6f8b7d4d315338423196ce1c4251ede5a8c9c1dfff645f67a008935", "Public key"),
                            new RouteSample("whatisit/356facdac5f5bcae995d13e667bb5864fd1e7d59", "Hash pub key"), 
                            new RouteSample("whatisit/OP_DUP OP_HASH160 356facdac5f5bcae995d13e667bb5864fd1e7d59 OP_EQUALVERIFY OP_CHECKSIG","Script"), 
                            new RouteSample("whatisit/76a914356facdac5f5bcae995d13e667bb5864fd1e7d5988ac","Script bytes"), 
                            
                            new RouteSample("whatisit/3P2sV4w1ZSk5gr6eePd6U2V56Mx5fT3RkD","P2SH Address"),
                            new RouteSample("whatisit/ea1bea7de1b975b962adbd57a9e0533449962a80", "Script Hash"),
                            new RouteSample("whatisit/2103f670154f21dd26f558a5718776b3905d19ee83b01592255ea7b472d52d09d8baac", "Script bytes"),

                            new RouteSample("whatisit/d3294938d12105a27a55af9c02864d6f633741bd5c5340a18972935a457275b9","Transaction id"),

                            new RouteSample("whatisit/0000000000000000080feeba134552a002aeb08e815aad1c41108f680d7b5f58","Block id"),
                            new RouteSample("whatisit/0100000000000000000000000000000000000000000000000000000000000000000000003ba3edfd7a7b12b27ac72c3e67768f617fc81bc3888a51323a9fb8aa4b1e5e4a29ab5f49ffff001d1dac2b7c","Block header"),
                            new RouteSample("whatisit/335598", "Block height"), //Height
                            new RouteSample("whatisit/3045022100a8a45e762fbda89f16a08de25274257eb2b7d9fbf481d359b28e47205c8bdc2f022007917ee618ae55a8936c75ad603623671f27ce8591010b769718ebc5ff295cf001","Signature")
                        }
                    }
                }
            });
        }
    }
}
