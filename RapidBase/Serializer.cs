using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RapidBase.JsonConverters;
using System.Net.Http.Formatting;

namespace RapidBase
{
    public class Serializer
    {
        public static void RegisterFrontConverters(JsonSerializerSettings settings)
        {
            settings.Converters.Add(new BitcoinSerializableJsonConverter());
            settings.Converters.Add(new MoneyJsonConverter());
            settings.Converters.Add(new CoinJsonConverter());
            settings.Converters.Add(new ScriptJsonConverter());
            settings.Converters.Add(new NetworkJsonConverter());
            settings.Converters.Add(new BalanceLocatorJsonConverter());
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        }

        public static T ToObject<T>(string data)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };
            RegisterFrontConverters(settings);
            return JsonConvert.DeserializeObject<T>(data, settings);
        }

        public static string ToString<T>(T response)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };
            RegisterFrontConverters(settings);
            return JsonConvert.SerializeObject(response, settings);
        }

        public static MediaTypeFormatter JsonMediaTypeFormatter
        {
            get
            {
                var mediaFormat = new JsonMediaTypeFormatter();
                Serializer.RegisterFrontConverters(mediaFormat.SerializerSettings);
                mediaFormat.Indent = true;
                return mediaFormat;
            }
        }
    }
}
