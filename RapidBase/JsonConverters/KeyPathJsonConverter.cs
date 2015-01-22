using NBitcoin;
using System;
using System.Reflection;
using Newtonsoft.Json;


namespace RapidBase.JsonConverters
{
    public class KeyPathJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(KeyPath).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;
            return new KeyPath(reader.Value.ToString());
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var keyPath = value as KeyPath;
            if (keyPath != null)
                writer.WriteValue(keyPath.ToString());
        }
    }
}
