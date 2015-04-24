using Newtonsoft.Json;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBitNinja.JsonConverters
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
    public class EnumAliasJsonConverter : JsonConverter
    {
        Dictionary<string, object> _Values = null;
        void EnsureInit(Type type)
        {
            if (_Values != null)
                return;
            _Values = new Dictionary<string, object>();
            foreach (var member in type.GetFields())
            {
                var alias = member.GetCustomAttribute<EnumAliasAttribute>();
                if (alias != null)
                {
                    _Values.Add(alias.Name, (object)Enum.Parse(type, member.Name.Split().Last()));
                }
            }
        }
        public override bool CanConvert(Type objectType)
        {
            return objectType.IsEnum;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            EnsureInit(objectType);
            var val = (string)reader.Value;
            object result;
            if (!_Values.TryGetValue(val, out result))
            {
                throw new JsonObjectException("Invalid notification type, available are " + string.Join(",", _Values.Select(kv => kv.Key).ToArray()), reader);
            }
            return result;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
                return;
            EnsureInit(value.GetType());
            var str = _Values.FirstOrDefault(v => v.Value.Equals((object)value));
            if (str.Equals(default(KeyValuePair<string, object>)))
                throw new NotSupportedException(value.ToString());
            writer.WriteValue(str.Key);
        }
    }
}
