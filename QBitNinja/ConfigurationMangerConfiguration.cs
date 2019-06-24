using System.Collections.Generic;
using System.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace QBitNinja
{
    public class ConfigurationManagerConfiguration : IConfiguration
    {
        public string this[string key]
        {
            get => ConfigurationManager.AppSettings[key];
            set => ConfigurationManager.AppSettings[key] = value;
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