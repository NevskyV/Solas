using System.Diagnostics;
using System.Reflection;
using Solas.Settings;

namespace Solas.Build;

public static class Program
{
    public static void Main(string[] _)
    {
        var editorVfs = new VirtualFileSystem(Directory.GetParent(BuildConfig.GameProjectPath)?.FullName);
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
                $"build \"{BuildConfig.GameProjectPath}\" -o {editorVfs.GetMountPath("build")} --configuration Release",
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
            Path.GetFileNameWithoutExtension(BuildConfig.GameProjectPath) ?? "Game.dll");
        Assembly.LoadFrom(gameDll);

        Engine.SetSerializer(BuildConfig.SerializerName);

        Engine.SetVfs(editorVfs);

        Engine.LoadEngineSettings(editorVfs.GetPath("engine://Settings"));

        var runtimeVfs = new VirtualFileSystem(Query.GetSettings<BuildSettings>().OutputDirectory);
        runtimeVfs.Mount("assets", "Assets");
        runtimeVfs.Mount("engine", "Solas");

        Engine.EnsureNeededDirectories(
            runtimeVfs.GetMountPath("assets"),
            runtimeVfs.GetMountPath("engine"),
            runtimeVfs.GetPath("engine://Settings"));

        new BuildPipeline(editorVfs, runtimeVfs).BuildAsync().GetAwaiter().GetResult();

        Directory.Delete(editorVfs.GetMountPath("build"), true);
    }
}