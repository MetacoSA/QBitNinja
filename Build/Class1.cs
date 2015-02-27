using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Serialization;

namespace Build
{
    public class CreateNuspec : Task
    {
        public string OriginalNuspec
        {
            get;
            set;
        }

        public string ModifiedNuspec
        {
            get;
            set;
        }

        public string AssemblyFile
        {
            get;
            set;
        }

        public string Configuration
        {
            get;
            set;
        }

        public ITaskItem[] Projects
        {
            get;
            set;
        }

        public override bool Execute()
        {
            var manifest = Manifest.ReadFrom(new MemoryStream(File.ReadAllBytes(OriginalNuspec)), true);
            var assemblyFile = File.ReadAllText(AssemblyFile);

            manifest.Metadata.Version = Find(assemblyFile, "AssemblyVersion");
            NetPortableProfileTable.GetProfile("lol"); //prevent bug of concurrency on the table
            var tasks = Projects
                .Select(i => System.Threading.Tasks.Task.Run(() =>
                {
                    ProjectCollection collection = new ProjectCollection(new Dictionary<string, string>()
                    {
                        {"Configuration", Configuration}
                    });
                    var project = collection.LoadProject(i.ItemSpec);
                    bool portable = false;
                    var prop = project.AllEvaluatedProperties.OfType<ProjectProperty>().FirstOrDefault(b => b.Name == "TargetFrameworkProfile");
                    string targetFramework = "net45";
                    if (prop != null)
                    {
                        targetFramework = FindMagicFreakingNugetString(prop.EvaluatedValue);
                        portable = true;
                    }
                    Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                    var instance = project.CreateProjectInstance();
                    var result = new BuildManager().Build(new BuildParameters(), new BuildRequestData(instance, new string[] { "GetTargetPath" }));
                    var dll = result.ResultsByTarget.First(_ => _.Key == "GetTargetPath").Value.Items.First().ItemSpec;
                    return new
                    {
                        Generated = dll,
                        PackageConfig = new PackageReferenceFile(Path.Combine(Path.GetDirectoryName(project.FullPath), "packages.config")),
                        TargetFramework = portable ? "portable-" + targetFramework : targetFramework,
                    };
                })).ToArray();

            try
            {
                var projects = tasks.Select(t => t.Result).ToArray();
                foreach (var project in projects)
                {
                    var dependencies = new ManifestDependencySet();
                    manifest.Metadata.DependencySets.Add(dependencies);
                    dependencies.TargetFramework = project.TargetFramework;
                    dependencies.Dependencies = new List<ManifestDependency>();
                    foreach (var dep in project.PackageConfig.GetPackageReferences())
                    {
                        dependencies.Dependencies.Add(new ManifestDependency()
                        {
                            Id = dep.Id,
                            Version = dep.Version.ToString()
                        });
                    }
                    manifest.Files.Add(new ManifestFile()
                    {
                        Target = "lib\\" + project.TargetFramework,
                        Source = project.Generated
                    });
                }
                MemoryStream ms = new MemoryStream();
                manifest.Save(ms, true);
                ms.Position = 0;
                var result = new StreamReader(ms).ReadToEnd();
                File.WriteAllText(ModifiedNuspec, result);
            }
            catch (AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
            }
            return true;
        }

        private string FindMagicFreakingNugetString(string profile)
        {
            if (profile == "Profile259")
                return "net45+win+wpa81+wp80+Xamarin.iOS10+MonoAndroid10+MonoTouch10";
            if (profile == "Profile111")
                return "net45+win+wpa81+Xamarin.iOS10+MonoAndroid10+MonoTouch10";
            var prof = NetPortableProfileTable.GetProfile(profile);
            if(prof == null)
                throw new NotSupportedException("Profile not supported " + profile);
            return prof.CustomProfileString;
        }

        private string Find(string file, string attribute)
        {
            var match = Regex.Match(file, "\\[assembly: " + attribute + "\\(\"(.*?)\"\\)\\]");
            var value = match.Groups[1].Value;
            return value;
        }
    }
}
