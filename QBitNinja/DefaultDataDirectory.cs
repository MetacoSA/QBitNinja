using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QBitNinja
{
    public static class DefaultDataDirectory
    {
		/// <summary>
		/// Get or create a directory rooted in a location that is specified as an environment variable 
		/// named either 'HOME', or 'APPDATA' (fallback).
		/// </summary>
		public static string GetDirectory(string appDirectory, string subDirectory, bool createIfNotExists = true)
		{
			string directory = null;

			// Find path to 'appDirectory'.
			var home = Environment.GetEnvironmentVariable("HOME");
			if (!string.IsNullOrEmpty(home))
			{
				directory = home;
				directory = Path.Combine(directory, "." + appDirectory.ToLowerInvariant());
			}
			else
			{
				var localAppData = Environment.GetEnvironmentVariable("APPDATA");
				if (!string.IsNullOrEmpty(localAppData))
				{
					directory = localAppData;
					directory = Path.Combine(directory, appDirectory);
				}
				else
				{
					throw new DirectoryNotFoundException("Could not find suitable datadir.");
				}
			}


			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			// Find path to 'subdirectory' and create it if needed.
			directory = Path.Combine(directory, subDirectory);
			if (createIfNotExists && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			return directory;
		}
	}
}
