using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.Configuration;

namespace QBitNinja
{
    public class ConfigurationManagerConfiguration : IConfiguration
    {
        public string this[string key]
        {
            get
            {
                return ConfigurationManager.AppSettings[key];
            }
            set
            {
                ConfigurationManager.AppSettings[key] = value;
            }
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            return new IConfigurationSection[0];
        }

        public IChangeToken GetReloadToken()
        {
            return null;
        }

        public IConfigurationSection GetSection(string key)
        {
            return null;
        }
    }
}