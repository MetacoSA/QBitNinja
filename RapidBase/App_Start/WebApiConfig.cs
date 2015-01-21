using System;
using System.Net;
using System.Net.Http.Formatting;
using System.Web.Http;
using System.Web.Http.Validation;

namespace RapidBase
{
    public static class WebApiConfig
    {
        class NoBodyModelProvider : IBodyModelValidator
        {
            #region IBodyModelValidator Members

            public bool Validate(object model, Type type, System.Web.Http.Metadata.ModelMetadataProvider metadataProvider, System.Web.Http.Controllers.HttpActionContext actionContext, string keyPrefix)
            {
                return true;
            }

            #endregion
        }
        public static void Register(HttpConfiguration config)
        {
            Register(config, null);
        }

        internal static void SetThrottling()
        {
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 1000;
        }

        public static void Register(HttpConfiguration config, RapidBaseConfiguration rapidbase)
        {
            SetThrottling();
            if (rapidbase == null)
                rapidbase = RapidBaseConfiguration.FromConfiguration();
            rapidbase.EnsureSetup();
            config.MapHttpAttributeRoutes();
            config.Formatters.Clear();
            config.Formatters.Add(new JsonMediaTypeFormatter()
            {
                Indent = true
            });
            config.DependencyResolver = new RapidBaseDependencyResolver(rapidbase, config.DependencyResolver);
            config.Filters.Add(new GlobalExceptionFilter());
            config.Services.Replace(typeof(IBodyModelValidator), new NoBodyModelProvider());
            Serializer.RegisterFrontConverters(config.Formatters.JsonFormatter.SerializerSettings, rapidbase.Indexer.Network);
        }
    }
}
