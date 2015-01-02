using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using System;

namespace RapidBase.JsonConverters
{
    public class ScriptJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(Script).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return reader.TokenType == JsonToken.Null ? null : Script.FromBytesUnsafe(Encoders.Hex.DecodeData((string)reader.Value));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(Encoders.Hex.EncodeData(((Script)value).ToBytes(false)));
        }
    }
}
