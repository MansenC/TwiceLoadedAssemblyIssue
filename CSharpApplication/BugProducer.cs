using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Build.Framework;

namespace Example
{
    // What a great name for a class
    public static class BugProducer
    {
        /// <summary>
        ///     Statically initializes the usage of MSBuild
        /// </summary>
        public static void StaticInit()
        {
            LoadNuGetSdkResolver();
            UseMSBuildFrameworkClass();
        }

        /// <summary>
        ///     This loads the MSBuild.NuGetSdkResolver dll and calls GetExportedTypes on that assembly.
        ///     That loads the MSBuild.Framework.dll into the default ALC
        /// </summary>
        private static void LoadNuGetSdkResolver()
        {
            string path = Path.GetFullPath(".");
            if (!path.Contains("x64"))
            {
                path = Path.Combine("../x64/Debug");
            }

            path = Path.GetFullPath(path);

            string dllPath = Path.Combine(path, "Microsoft.Build.NuGetSdkResolver.dll");
            Console.WriteLine("Trying to load " + dllPath);

            Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
            Console.WriteLine($"Loaded NuGet assembly with hash: {assembly.GetHashCode()}");
            Console.WriteLine($"Types: {assembly.ExportedTypes}");
        }

        /// <summary>
        ///     This implicitly calls ALC.Resolve for the MSBuild.Framework dll, which in turn loads it
        ///     a second time into the default ALC
        /// </summary>
        private static void UseMSBuildFrameworkClass()
        {
            BuildStartedEventArgs args = new("", "");
            Console.WriteLine($"Created args: {args}");
        }
    }
}
