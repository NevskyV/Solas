using System.Diagnostics;
using System.Reflection;
using Solas.Settings;

namespace Solas.Build;

public static class Program
{
    public static void Main(string[] args)
    {
        var projectPath = args[0];
        var serializerName = args.Length < 2 || String.IsNullOrEmpty(args[1])
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
                $"build \"{projectPath}\" -o {editorVfs.GetMountPath("build")} --configuration Release",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process())
        {
            process.StartInfo = startInfo;
            Console.WriteLine("Building project...");
            process.Start();

            process.WaitForExit();
        }

        var gameDll = Path.Combine(editorVfs.GetMountPath("build"),
            Path.GetFileNameWithoutExtension(projectPath) ?? "Game.dll");
        Assembly.LoadFrom(gameDll);

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

        new BuildPipeline(editorVfs, runtimeVfs, projectPath).BuildAsync().GetAwaiter().GetResult();

        Directory.Delete(editorVfs.GetMountPath("build"), true);
    }
}