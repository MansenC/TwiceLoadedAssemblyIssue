# Reproduction of the magically double-loaded assembly

This issue loads the assembly `Microsoft.Build.Framework` twice into the same HostContext and therefor creating a lot of issues that are torturing me.

Good to know:
- If you open the solution, set CSharpApplication as the startup project. Run that and follow the console output, you should see that everything works just fine
- Set the NativeHost project as startup and you should see that the issue arises, as documented
- *You may have to adjust the Include Directories and External Include Directories in the properties of the NativeHost project*. I statically linked them to my installation of dotnet but that may be different for you. If you don't have the sdk installed in the Program Files and don't use use version 6.0.3 for win-x64 you have to change that. (NativeHost > Properties > VC++ Directories)