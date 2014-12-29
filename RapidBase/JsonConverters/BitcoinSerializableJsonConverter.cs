using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RapidBase.JsonConverters
{
    public class BitcoinSerializableJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(IBitcoinSerializable).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;


            return Activator.CreateInstance(objectType, new[] { reader.Value });
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var inverse = value is uint256 || value is uint160;
            var bytes = ((IBitcoinSerializable)value).ToBytes();
            if (inverse)
                Array.Reverse(bytes);
            writer.WriteValue(Encoders.Hex.EncodeData(bytes));
        }
    }
}