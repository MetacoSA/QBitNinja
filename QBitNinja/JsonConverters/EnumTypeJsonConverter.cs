using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

#if !CLIENT
namespace QBitNinja.JsonConverters
#else
namespace QBitNinja.Client.JsonConverters
#endif
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
#if !NOJSONNET
	public
#else
	internal
#endif
	class EnumTypeJsonConverter : JsonConverter
    {
        private Dictionary<string, TypeInfo> _Values;
        private TypeInfo _EnumType;

        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            EnsureInit(objectType.GetTypeInfo());
            JObject jObj = serializer.Deserialize<JObject>(reader);
            string typeEnum = serializer.Deserialize(jObj.Property("type").Value.CreateReader(), _EnumType.AsType()).ToString();
            if (!_Values.TryGetValue(typeEnum, out TypeInfo type))
            {
                throw new NotSupportedException(typeEnum);
            }

            JsonReader jobjReader = jObj.CreateReader();
            return ReadJsonBase(serializer, type, jobjReader);
        }


        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
        }

        private static object ReadJsonBase(JsonSerializer serializer, TypeInfo type, JsonReader reader)
        {
            if (reader.TokenType == JsonToken.None)
            {
                reader.Read();
            }

            JsonContract contract = serializer.ContractResolver.ResolveContract(type.AsType());
            MethodInfo getInternalSerializer = serializer.GetType().GetTypeInfo().GetDeclaredMethod("GetInternalSerializer");
            object internalSerializer = getInternalSerializer.Invoke(serializer, new object[0]);
            MethodInfo createValue = internalSerializer.GetType().GetTypeInfo().GetDeclaredMethod("CreateValueInternal");
            object result = createValue.Invoke(internalSerializer, new object[] { reader, type, contract, null, null, null, null });
            return result;
        }

        private void EnsureInit(TypeInfo type)
        {
            if (_Values != null)
            {
                return;
            }

            _Values = new Dictionary<string, TypeInfo>();
            foreach (EnumTypeAttribute attribute in type.GetCustomAttributes(typeof(EnumTypeAttribute)).OfType<EnumTypeAttribute>())
            {
                string key = attribute.Value.ToString();
                TypeInfo value = attribute.Type.GetTypeInfo();
                _EnumType = attribute.Value.GetType().GetTypeInfo();
                _Values.Add(key, value);
            }
        }
    }
}
