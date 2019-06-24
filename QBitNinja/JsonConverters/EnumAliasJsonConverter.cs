using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

#if !CLIENT
namespace QBitNinja.JsonConverters
#else
namespace QBitNinja.Client.JsonConverters
#endif
{
    public class EnumAliasAttribute : Attribute
    {
        public EnumAliasAttribute(string name)
        {
            Name = name;
        }
        public string Name
        {
            get;
            set;
        }
    }

	/// <summary>
	/// Convert string to enum properties from their EnumAliasAttribute (See SubscriptionType)
	/// </summary>
#if !NOJSONNET
	public
#else
	internal
#endif
	class EnumAliasJsonConverter : JsonConverter
    {
        private Dictionary<string, object> _Values;

        public override bool CanConvert(Type objectType) => objectType.GetTypeInfo().IsEnum;

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            EnsureInit(objectType);
            var val = (string)reader.Value;
            if (!_Values.TryGetValue(val, out object result))
            {
                throw new JsonObjectException("Invalid notification type, available are " + string.Join(",", _Values.Select(kv => kv.Key).ToArray()), reader);
            }

            return result;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                return;
            }

            EnsureInit(value.GetType());
            KeyValuePair<string, object> str = _Values.FirstOrDefault(v => v.Value.Equals(value));
            if (str.Equals(default(KeyValuePair<string, object>)))
            {
                throw new NotSupportedException(value.ToString());
            }

            writer.WriteValue(str.Key);
        }

        private void EnsureInit(Type type)
        {
            if (_Values != null)
            {
                return;
            }

            _Values = new Dictionary<string, object>();
            foreach (FieldInfo member in type.GetTypeInfo().DeclaredFields)
            {
                EnumAliasAttribute alias = member.GetCustomAttribute<EnumAliasAttribute>();
                if (alias != null)
                {
                    _Values.Add(alias.Name, (object)Enum.Parse(type, member.Name.Split().Last()));
                }
            }
        }
    }
}
