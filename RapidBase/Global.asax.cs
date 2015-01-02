using System;
using System.Threading;
using System.Web;
using System.Web.Http;
using System.Web.Routing;

namespace RapidBase
{
    public class WebApiApplication : HttpApplication
    {

        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            Resolver = (RapidBaseDependencyResolver)GlobalConfiguration.Configuration.DependencyResolver;
            Timer = new Timer(_ => Resolver.UpdateChain());
            Timer.Change(0, (int)TimeSpan.FromSeconds(30).TotalMilliseconds);
        }
        protected void Application_End()
        {
            Timer.Dispose();
        }

        internal Timer Timer
        {
            get;
            set;
        }
        internal RapidBaseDependencyResolver Resolver
        {
            get;
            set;
        }
    }
}
