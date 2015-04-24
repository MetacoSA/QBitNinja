using Newtonsoft.Json;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace QBitNinja.JsonConverters
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class EnumTypeAttribute : Attribute
    {
        public EnumTypeAttribute(object value, Type type)
        {
            Value = value;
            Type = type;
        }
        public object Value
        {
            get;
            set;
        }
        public Type Type
        {
            get;
            set;
        }
    }


    /// <summary>
    /// Deserialize a derived class thanks to an enum indicator member field (see Subscription)
    /// </summary>
    public class EnumTypeJsonConverter : JsonConverter
    {
        Dictionary<string, Type> _Values = null;
        Type _EnumType;
        void EnsureInit(Type type)
        {
            if (_Values != null)
                return;
            _Values = new Dictionary<string, Type>();
            foreach (var attribute in type.GetCustomAttributes(typeof(EnumTypeAttribute)).OfType<EnumTypeAttribute>())
            {
                var key = attribute.Value.ToString();
                var value = attribute.Type;
                _EnumType = attribute.Value.GetType();
                if (key != null)
                {
                    _Values.Add(key, value);
                }

            }
        }
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;
            EnsureInit(objectType);
            var jobj = serializer.Deserialize<JObject>(reader);

            var typeEnum = serializer.Deserialize(jobj.Property("type").Value.CreateReader(), _EnumType).ToString();
            Type type;
            if (!_Values.TryGetValue(typeEnum, out type))
            {
                throw new NotSupportedException(typeEnum.ToString());
            }

            var jobjReader = jobj.CreateReader();
            return ReadJsonBase(serializer, type, jobjReader);
        }

        private static object ReadJsonBase(JsonSerializer serializer, Type type, JsonReader reader)
        {
            if (reader.TokenType == JsonToken.None)
                reader.Read();
            var contract = serializer.ContractResolver.ResolveContract(type);
            var getInternalSerializer = serializer.GetType().GetMethod("GetInternalSerializer", BindingFlags.NonPublic | BindingFlags.Instance);
            var internalSerializer = getInternalSerializer.Invoke(serializer, new object[0]);
            var createValue = internalSerializer.GetType().GetMethod("CreateValueInternal", BindingFlags.NonPublic | BindingFlags.Instance);
            var result = createValue.Invoke(internalSerializer, new object[] { reader, type, contract, null, null, null, null });
            return result;
        }


        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
        }
    }
}
