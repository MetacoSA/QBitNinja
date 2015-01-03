using System.Web.Mvc;
using System.Web.Routing;

namespace RapidBase
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.MapRoute(
                name: "Default",
                url: "",
                defaults: new { controller="Help", action = "Index" }
            );
        }
    }
}

