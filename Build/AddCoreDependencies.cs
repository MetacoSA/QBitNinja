using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Build
{
	public class AddCoreDependencies : Task
	{
		public string InputFile
		{
			get;
			set;
		}

		public string ProjectJsonFile
		{
			get;
			set;
		}

		public string OutputFile
		{
			get;
			set;
		}

		public string TargetFramework
		{
			get;
			set;
		}

		public string FrameworkName
		{
			get;
			set;
		}

		public override bool Execute()
		{
			var projectProj = File.ReadAllText(ProjectJsonFile);
			XmlDocument doc = new XmlDocument();
			doc.LoadXml(projectProj);
			StringBuilder builder = new StringBuilder();
			foreach(var node in doc.SelectNodes("//PackageReference").OfType<XmlNode>())
			{
				AddDependency(builder, node);
			}

			var nuspec = File.ReadAllText(InputFile);
			var group = "<group targetFramework=\"" + TargetFramework + "\">\r\n";
			nuspec = nuspec.Replace(group, group + builder.ToString());
			File.WriteAllText(OutputFile, nuspec);
			return true;
		}

		private static void AddDependency(StringBuilder builder, XmlNode dep)
		{
			builder.AppendLine("<dependency id=\"" + dep.Attributes["Include"].Value + "\" version=\"[" + dep.Attributes["Version"].Value + ", )\" />");
		}
	}
}
