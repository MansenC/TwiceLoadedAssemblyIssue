namespace Example
{
    public class ExampleEntrypoint
    {
        public delegate void EntrypointDelegate();    

        public static void Entrypoint()
        {
            AppDomain.CurrentDomain.AssemblyLoad += (_, args) =>
            {
                if (args.LoadedAssembly.GetName().Name != "Microsoft.Build.Framework")
                {
                    return;
                }

                Console.WriteLine($"Loaded {args.LoadedAssembly.FullName} => {Environment.StackTrace}");
            };

            RecompilerService.StaticInit();

            var solutionPath = Path.Combine(AppContext.BaseDirectory, "../../RestorableProject/CSProject.sln");
            Console.WriteLine($"Attempting to restore project at {solutionPath}");

            RecompilerService.RestoreSolutionAt(solutionPath).Wait();
        }

        internal static void Main(string[] args)
        {
            Entrypoint();
        }
    }
}
