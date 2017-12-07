using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QBitNinja
{
    public class DefaultDataDirectory
    {
		public static string GetDirectory(string appDirectory, string subDirectory, bool createIfNotExists = true)
		{
			string directory = null;
			var home = Environment.GetEnvironmentVariable("HOME");
			if(!string.IsNullOrEmpty(home))
			{
				directory = home;
				directory = Path.Combine(directory, "." + appDirectory.ToLowerInvariant());
			}
			else
			{
				var localAppData = Environment.GetEnvironmentVariable("APPDATA");
				if(!string.IsNullOrEmpty(localAppData))
				{
					directory = localAppData;
					directory = Path.Combine(directory, appDirectory);
				}
				else
				{
					throw new DirectoryNotFoundException("Could not find suitable datadir");
				}
			}
			if(!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			directory = Path.Combine(directory, subDirectory);
			if(createIfNotExists && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			return directory;
		}
	}
}
