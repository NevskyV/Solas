using System.Diagnostics;
using System.Reflection;
using System.Security.AccessControl;
using Solas.Settings;

namespace Solas.Build;

public static class Program
{
    public static void Main(string[] args)
    {
        var projectPath = args[0];
        var serializerName = args.Length < 2 || string.IsNullOrEmpty(args[1])
            ? "Solas.Serialization.Json.EngineJsonSerializer, Core"
            : args[1];
        var editorVfs = new VirtualFileSystem(Directory.GetParent(projectPath)?.FullName);
        editorVfs.Mount("assets", "Assets");
        editorVfs.Mount("engine", "Solas");
        editorVfs.Mount("build", "Build");
        Engine.EnsureNeededDirectories(
            editorVfs.GetMountPath("assets"),
            editorVfs.GetMountPath("engine"),
            editorVfs.GetMountPath("build"),
            editorVfs.GetPath("engine://Settings"));

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments =
                $"build \"{projectPath}\" -o {editorVfs.GetMountPath("build")} -c Release",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process())
        {
            process.StartInfo = startInfo;
            Console.WriteLine("Building project assembly...");
            process.Start();

            process.WaitForExit();
        }

        var gameDll = Directory.GetFiles(Path.Combine(editorVfs.GetMountPath("build"))).First(x=>x.EndsWith(".exe")).Replace("exe", "dll");
        Assembly.LoadFrom(gameDll);
        Console.WriteLine("Assembly loaded.");
        
        Engine.SetSerializer(serializerName);

        Engine.SetVfs(editorVfs);

        Engine.LoadEngineSettings(editorVfs.GetPath("engine://Settings"));

        var runtimeVfs = new VirtualFileSystem(Query.GetSettings<BuildSettings>().OutputDirectory);
        runtimeVfs.Mount("assets", "Assets");
        runtimeVfs.Mount("engine", "Solas");

        Engine.EnsureNeededDirectories(
            runtimeVfs.GetMountPath("assets"),
            runtimeVfs.GetMountPath("engine"),
            runtimeVfs.GetPath("engine://Settings"));

        Console.WriteLine("Directories OK.");
        
        new BuildPipeline(editorVfs, runtimeVfs, projectPath).BuildAsync().GetAwaiter().GetResult();

        Console.WriteLine("Cleanup...");
        try
        {
            Directory.Delete(editorVfs.GetMountPath("build"), true);
            Console.WriteLine("Cleanup OK.");
        }
        catch(Exception e)
        {
            Console.WriteLine($"❗Caught exception on cleanup: {e}");
        }
        Console.WriteLine("✅ Your project build successfully");
    }
}