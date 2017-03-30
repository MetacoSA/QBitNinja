﻿using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
#if !CLIENT
using QBitNinja.JsonConverters;
#else
using QBitNinja.Client.JsonConverters;
#endif
#if !CLIENT
using System.Net.Http.Formatting;
#endif

#if !CLIENT
namespace QBitNinja
#else
namespace QBitNinja.Client
#endif
{
    public class Serializer
    {
#if !NOJSONNET
		public
#else
		internal
#endif
		static void RegisterFrontConverters(JsonSerializerSettings settings, Network network = null)
        {
            settings.Converters.Add(new BitcoinSerializableJsonConverter());
            settings.Converters.Add(new MoneyJsonConverter());
            settings.Converters.Add(new CoinJsonConverter(network));
            settings.Converters.Add(new ScriptJsonConverter());
            settings.Converters.Add(new UInt160JsonConverter());
            settings.Converters.Add(new UInt256JsonConverter());
            settings.Converters.Add(new NetworkJsonConverter());
            settings.Converters.Add(new KeyPathJsonConverter());
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
            return ToObject<T>(data, null);
        }
        public static T ToObject<T>(string data, Network network)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };
            RegisterFrontConverters(settings, network);
            return JsonConvert.DeserializeObject<T>(data, settings);
        }

        public static string ToString<T>(T response, Network network)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };
            RegisterFrontConverters(settings, network);
            return JsonConvert.SerializeObject(response, settings);
        }
        public static string ToString<T>(T response)
        {
            return ToString<T>(response, null);
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
