using System.Collections.Immutable;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Locator;
using MSBuildProject = Microsoft.Build.Evaluation.Project;

namespace Example
{
    public static class RecompilerService
    {
        private static ProjectCollection BatchCollection { get; set; }

        private static SemaphoreSlim BuildManagerLock { get; set; } = new(1);

        // Same properties as https://github.com/dotnet/roslyn/blob/main/src/Workspaces/Core/MSBuild/MSBuild/Build/ProjectBuildManager.cs#L28
        // You can guess where I got the rest of this code
        private static readonly ImmutableDictionary<string, string> _defaultGlobalProperties = new Dictionary<string, string>()
        {
            { "DesignTimeBuild", bool.TrueString },
            { "NonExistentFile", "__NonExistentSubDir__\\__NonExistentFile__" },
            { "BuildProjectReferences", bool.FalseString },
            { "BuildingProject", bool.FalseString },
            { "ProvideCommandLineArgs", bool.TrueString },
            { "SkipCompilerExecution", bool.TrueString },
            { "ContinueOnError", "ErrorAndContinue" },
            { "ShouldUnsetParentConfigurationAndPlatform", bool.FalseString }
        }.ToImmutableDictionary();

        private static readonly XmlReaderSettings _xmlReaderSettings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        /// <summary>
        ///     Statically initializes the usage of MSBuild
        /// </summary>
        public static void StaticInit()
        {
            var sdkPath = AppContext.BaseDirectory;
            Console.WriteLine($"Loading MSBuild sdk at {sdkPath}");
            RegisterMSBuildEnvironmentVariables(sdkPath);
            MSBuildLocator.RegisterMSBuildPath(sdkPath);
        }

        /// <summary>
        ///     This attempts to restore a solution given a target path
        /// </summary>
        /// <param name="solutionPath"></param>
        /// <returns></returns>
        public static async Task RestoreSolutionAt(string solutionPath)
        {
            Console.WriteLine("========== This should trigger the first assembly load");
            StartBatchBuild();

            var solutionFile = SolutionFile.Parse(solutionPath);
            foreach (var solutionProject in solutionFile.ProjectsInOrder)
            {
                var msbuildProject = LoadProject(solutionProject.AbsolutePath);
                Console.WriteLine("========== Project loaded. Assembly should now be loaded twice");
                await RestoreProject(msbuildProject);
            }

            EndBatchBuild();
        }

        /// <summary>
        ///     Starts a build using the DefaultBuildManager
        /// </summary>
        private static void StartBatchBuild()
        {
            Console.WriteLine("========== Assembly should now be loaded once");

            BatchCollection = new ProjectCollection(_defaultGlobalProperties);
            var buildParameters = new BuildParameters(BatchCollection)
            {
                Loggers = new[]
                {
                    new MSBuildLogger()
                }
            };

            BuildManager.DefaultBuildManager.BeginBuild(buildParameters);
        }

        /// <summary>
        ///     Ends a build in the DefaultBuildManager
        /// </summary>
        private static void EndBatchBuild()
        {
            BuildManager.DefaultBuildManager.EndBuild();
            BatchCollection?.UnloadAllProjects();
            BatchCollection = null;
        }

        /// <summary>
        ///     Loads a project using the path. Used to call builds on that project using MSBuild
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static MSBuildProject LoadProject(string path)
        {
            var loadedProjects = BatchCollection?.GetLoadedProjects(path);
            if (loadedProjects != null && loadedProjects.Count > 0)
            {
                return loadedProjects.Single();
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            using var xmlReader = XmlReader.Create(stream, _xmlReaderSettings);

            var xml = ProjectRootElement.Create(xmlReader, BatchCollection);
            xml.FullPath = path;

            Console.WriteLine("========== This should trigger the second assembly load");
            return new MSBuildProject(xml, globalProperties: null, toolsVersion: null, BatchCollection);
        }

        /// <summary>
        ///     Calls a restore on the given project using MSBuild
        /// </summary>
        /// <param name="project"></param>
        /// <returns></returns>
        private static async Task RestoreProject(MSBuildProject project)
        {
            var targets = new[] { "Restore" };
            var projectInstance = project.CreateProjectInstance();
            foreach (var target in targets)
            {
                if (projectInstance.Targets.ContainsKey(target))
                {
                    continue;
                }

                Console.WriteLine($"Could not find target {target} in project");
                return;
            }

            var buildRequestData = new BuildRequestData(projectInstance, targets);

            CollectAssemblyData();

            Console.WriteLine("This should now fail if ran by the native host");
            var result = await BuildAsync(buildRequestData).ConfigureAwait(false);
            if (result.OverallResult != BuildResultCode.Failure)
            {
                return;
            }

            if (result.Exception == null)
            {
                Console.WriteLine("Build failed without an exception");
            }
            else
            {
                throw result.Exception;
            }
        }

        private static async Task<BuildResult> BuildAsync(BuildRequestData requestData)
        {
            using (await DisposableWaitAsync(BuildManagerLock).ConfigureAwait(false))
            {
                return await BuildAsync(
                    BuildManager.DefaultBuildManager,
                    requestData).ConfigureAwait(false);
            }
        }

        private static Task<BuildResult> BuildAsync(
            BuildManager buildManager,
            BuildRequestData requestData)
        {
            var taskSource = new TaskCompletionSource<BuildResult>();
            try
            {
                buildManager.PendBuildRequest(requestData).ExecuteAsync(sub =>
                {
                    try
                    {
                        var result = sub.BuildResult;
                        taskSource.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        taskSource.TrySetException(ex);
                    }
                }, null);
            }
            catch (Exception ex)
            {
                taskSource.SetException(ex);
            }

            return taskSource.Task;
        }

        private static async ValueTask<SemaphoreDisposer> DisposableWaitAsync(SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            return new SemaphoreDisposer(semaphore);
        }

        /// <summary>
        ///     Registers several environment variables that MSBuild uses to determine locations
        /// </summary>
        /// <param name="msbuildPath">The path where the MSBuild binaries are located</param>
        private static void RegisterMSBuildEnvironmentVariables(string msbuildPath)
        {
            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", Path.Combine(msbuildPath, "MSBuild.dll"));
            Environment.SetEnvironmentVariable("MSBuildExtensionsPath", msbuildPath);
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", Path.Combine(msbuildPath, "Sdks"));
            Environment.SetEnvironmentVariable("MSBuildEnableWorkloadResolver", bool.FalseString);
        }

        /// <summary>
        ///     This function logs all instanceof of the Microsoft.Build.Framework assembly that are loaded
        /// </summary>
        private static void CollectAssemblyData()
        {
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in allAssemblies.Where(x => x.GetName().Name == "Microsoft.Build.Framework"))
            {
                Console.WriteLine($"{assembly.FullName}: {assembly.HostContext}");
            }
        }
    }
}
