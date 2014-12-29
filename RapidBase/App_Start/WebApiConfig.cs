using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace RapidBase
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            Register(config, null);
        }
        public static void Register(HttpConfiguration config, RapidBaseConfiguration rapidbase)
        {
            if (rapidbase == null)
                rapidbase = RapidBaseConfiguration.FromConfiguration();
            config.MapHttpAttributeRoutes();
            config.DependencyResolver = new RapidBaseDependencyResolver(rapidbase, config.DependencyResolver);
            Serializer.RegisterFrontConverters(config.Formatters.JsonFormatter.SerializerSettings);
        }
    }
}
