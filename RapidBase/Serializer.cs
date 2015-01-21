using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RapidBase.JsonConverters;
#if !CLIENT
using System.Net.Http.Formatting;
#endif

namespace RapidBase
{
    public class Serializer
    {
        public static void RegisterFrontConverters(JsonSerializerSettings settings, Network network = null)
        {
            settings.Converters.Add(new BitcoinSerializableJsonConverter());
            settings.Converters.Add(new MoneyJsonConverter());
            settings.Converters.Add(new CoinJsonConverter());
            settings.Converters.Add(new ScriptJsonConverter());
            settings.Converters.Add(new NetworkJsonConverter());
            settings.Converters.Add(new Base58DataJsonConverter()
            {
                Network = network
            });
#if !CLIENT
            settings.Converters.Add(new BalanceLocatorJsonConverter());
#endif
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
#if !CLIENT
        public static MediaTypeFormatter JsonMediaTypeFormatter
        {
            get
            {
                var mediaFormat = new JsonMediaTypeFormatter();
                RegisterFrontConverters(mediaFormat.SerializerSettings);
                mediaFormat.Indent = true;
                return mediaFormat;
            }
        }
#endif
    }
}
