using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

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