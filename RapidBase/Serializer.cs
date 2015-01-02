using Newtonsoft.Json;
using RapidBase.JsonConverters;

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
        }

        public static string ToString<T>(T response)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings {Formatting = Formatting.Indented};
            RegisterFrontConverters(settings);
            return JsonConvert.SerializeObject(response, settings);
        }
    }
}
