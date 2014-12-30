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
            var model = new HelpModel()
            {
                Routes = ((IEnumerable<IHttpRoute>)GlobalConfiguration.Configuration.Routes[0])
                .Select(c => new RouteModel()
                {
                    Template = c.RouteTemplate,
                    Samples = GetSamples(c.RouteTemplate).ToArray()
                })
                .ToArray()
            };
            return View(model);
        }

        private IEnumerable<string> GetSamples(string str)
        {
            if (str.Contains("{blockFeature}"))
            {
                yield return str
                .Replace("{blockFeature}", "0000000000000000119fe3f65fd3038cbe8429ad2cf7c2de1e5e7481b34a01b4");
                yield return str
                .Replace("{blockFeature}", "326551");
                yield return str
                .Replace("{blockFeature}", "last");
            }
            else
            {
                yield return 
                    str
                .Replace("{blockId}", "0000000000000000119fe3f65fd3038cbe8429ad2cf7c2de1e5e7481b34a01b4")
                .Replace("{txId}", "d175571aeb13d5e30297d852ef640cd943333e18f40816538ac68b450412a9c5");
            }
        }
    }
}
