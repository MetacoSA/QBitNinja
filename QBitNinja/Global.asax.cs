using System.Web;
using System.Web.Http;
using System.Web.Routing;

namespace QBitNinja
{
    public class WebApiApplication : HttpApplication
    {
        UpdateChainListener _Listener;
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            _Listener = new UpdateChainListener();
            _Listener.Listen(GlobalConfiguration.Configuration);
        }
        protected void Application_End()
        {
            if (_Listener != null)
                _Listener.Dispose();
        }

       
    }
}
