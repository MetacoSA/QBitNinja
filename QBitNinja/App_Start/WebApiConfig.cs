using System;
using System.Net;
using System.Net.Http.Formatting;
using System.Web.Http;
using System.Web.Http.Validation;

namespace QBitNinja
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            Register(config, null);
        }

        public static void Register(HttpConfiguration config, QBitNinjaConfiguration QBitNinja, bool setup)
        {
            SetThrottling();
            QBitNinja = QBitNinja ?? QBitNinjaConfiguration.FromConfiguration(new ConfigurationManagerConfiguration());

            if (setup)
            {
                QBitNinja.EnsureSetup();
            }

            config.MapHttpAttributeRoutes();
            config.Formatters.Clear();
            config.Formatters.Add(new CustomJsonMediaTypeFormatter { Indent = true });
            config.DependencyResolver = new QBitNinjaDependencyResolver(QBitNinja, config.DependencyResolver);
            config.Filters.Add(new GlobalExceptionFilter());
            config.Services.Replace(typeof(IBodyModelValidator), new NoBodyModelProvider());
            config.MessageHandlers.Insert(0, new CompressionHandler());
            Serializer.RegisterFrontConverters(
                config.Formatters.JsonFormatter.SerializerSettings,
                QBitNinja.Indexer.Network);
        }

        public static void Register(HttpConfiguration config, QBitNinjaConfiguration QBitNinja)
        {
            Register(config, QBitNinja, true);
        }

        internal static void SetThrottling()
        {
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 1000;
        }

        /// <summary>
        /// The purpose of this class is to prevent serialization error to be silently catches by the web api
        /// </summary>
        private class CustomJsonMediaTypeFormatter : JsonMediaTypeFormatter
        {
            public override object ReadFromStream(
                Type type,
                System.IO.Stream readStream,
                System.Text.Encoding effectiveEncoding,
                IFormatterLogger formatterLogger)
            {
                return base.ReadFromStream(type, readStream, effectiveEncoding, null);
            }

            public override System.Threading.Tasks.Task<object> ReadFromStreamAsync(
                Type type,
                System.IO.Stream readStream,
                System.Net.Http.HttpContent content,
                IFormatterLogger formatterLogger)
            {
                return base.ReadFromStreamAsync(type, readStream, content, null);
            }

            public override System.Threading.Tasks.Task<object> ReadFromStreamAsync(
                Type type,
                System.IO.Stream readStream,
                System.Net.Http.HttpContent content,
                IFormatterLogger formatterLogger,
                System.Threading.CancellationToken cancellationToken)
            {
                return base.ReadFromStreamAsync(type, readStream, content, null, cancellationToken);
            }
        }

        private class NoBodyModelProvider : IBodyModelValidator
        {
            #region IBodyModelValidator Members

            public bool Validate(
                object model,
                Type type,
                System.Web.Http.Metadata.ModelMetadataProvider metadataProvider,
                System.Web.Http.Controllers.HttpActionContext actionContext,
                string keyPrefix)
            {
                return true;
            }

            #endregion
        }
    }
}
