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
            rapidbase.EnsureSetup();
            config.MapHttpAttributeRoutes();
            config.DependencyResolver = new RapidBaseDependencyResolver(rapidbase, config.DependencyResolver);
            config.Filters.Add(new GlobalExceptionFilter());
            Serializer.RegisterFrontConverters(config.Formatters.JsonFormatter.SerializerSettings);
        }
    }
}
