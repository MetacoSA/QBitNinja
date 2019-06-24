using System;
using System.IO;

namespace QBitNinja
{
    public class DefaultDataDirectory
    {
        public static string GetDirectory(string appDirectory, string subDirectory, bool createIfNotExists = true)
        {
            string directory;
            string home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                directory = Path.Combine(home, "." + appDirectory.ToLowerInvariant());
            }
            else
            {
                string localAppData = Environment.GetEnvironmentVariable("APPDATA");
                directory = string.IsNullOrEmpty(localAppData)
                    ? throw new DirectoryNotFoundException("Could not find suitable datadir")
                    : Path.Combine(localAppData, appDirectory);
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            directory = Path.Combine(directory, subDirectory);
            if (createIfNotExists && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return directory;
        }
    }
}
