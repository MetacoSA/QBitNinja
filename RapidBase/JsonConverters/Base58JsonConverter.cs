using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Reflection;

namespace RapidBase.JsonConverters
{
    public class Base58DataJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return
                typeof(Base58Data).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo()) ||
                typeof(IDestination).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var result = Base58Data.GetFromBase58Data(reader.Value.ToString());
            if (result == null)
            {
                throw new FormatException("Invalid base58 data" + reader.Path);
            }
            if (Network != null)
            {
                if (result.Network != Network)
                {
                    throw new FormatException("Invalid base58 network" + reader.Path);
                }
            }
            if (!objectType.GetTypeInfo().IsAssignableFrom(result.GetType().GetTypeInfo()))
            {
                throw new FormatException("Invalid base58 data " + reader.Path);
            }
            return result;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var base58 = value as Base58Data;
            if (base58 != null)
            {
                if (Network != null && base58.Network != Network)
                    throw new FormatException("Invalid base58 network");
                writer.WriteValue(value.ToString());
            }
        }

        public Network Network
        {
            get;
            set;
        }
    }
}
