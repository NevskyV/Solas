using Solas.Settings;

namespace Solas.Build;

public static class Program
{
    public static void Main(string[] _)
    {
        Engine.SetSerializer(BuildConfig.SerializerName);
    
        var editorVfs = new VirtualFileSystem(Directory.GetParent(BuildConfig.GameProjectPath)?.FullName);
        editorVfs.Mount("assets", "Assets");
        editorVfs.Mount("engine", "Solas");

        Engine.SetVfs(editorVfs);
        Engine.EnsureNeededDirectories(
            editorVfs.GetMountPath("assets"),
            editorVfs.GetMountPath("engine"),
            editorVfs.GetPath("engine://Settings"));
        Engine.LoadEngineSettings(editorVfs.GetPath("engine://Settings"));

        var runtimeVfs = new VirtualFileSystem(Query.GetSettings<BuildSettings>().OutputDirectory);
        runtimeVfs.Mount("assets", "Assets");
        runtimeVfs.Mount("engine", "Solas");
        Engine.EnsureNeededDirectories(
            runtimeVfs.GetMountPath("assets"),
            runtimeVfs.GetMountPath("engine"),
            runtimeVfs.GetPath("engine://Settings"));

        new BuildPipeline(editorVfs, runtimeVfs).BuildAsync().GetAwaiter().GetResult();
    }
}