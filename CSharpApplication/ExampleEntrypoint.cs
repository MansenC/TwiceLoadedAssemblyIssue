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

                Console.WriteLine($"========== Loaded {args.LoadedAssembly.FullName} => {args.LoadedAssembly.GetHashCode()}");
            };

            BugProducer.StaticInit();
        }

        internal static void Main(string[] args)
        {
            Entrypoint();
        }
    }
}
