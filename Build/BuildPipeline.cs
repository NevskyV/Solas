using System.Diagnostics;
using Solas.Components;
using Solas.Serialization.Binary;
using Solas.Serialization.Core;
using Solas.Settings;

namespace Solas.Build;

internal class BuildPipeline
{
    private readonly CoreSettings _coreSettings;
    private readonly BuildSettings _buildSettings;

    private readonly VirtualFileSystem _editorVfs;
    private readonly VirtualFileSystem _runtimeVfs;

    internal BuildPipeline(VirtualFileSystem editorVfs, VirtualFileSystem runtimeVfs)
    {
        _coreSettings = WorldContext.CoreSettings;
        _buildSettings = Query.GetSettings<BuildSettings>();
        
        _editorVfs = editorVfs;
        _runtimeVfs = runtimeVfs;
    }
    
    internal async Task BuildAsync()
    {
        await ProcessAssets();
        
        await PublishProject();
    }

    private async Task ProcessAssets()
    {
        var runtimeSerializer = (Serializer)Activator.CreateInstance(Type.GetType(_buildSettings.Serializer)!);

        if (runtimeSerializer == null) return;
        
        //Assets
        await using var assetsStream = File.OpenRead(_editorVfs.GetPath(_coreSettings.AssetsPackPath));
        await using var outAssetsStream = File.OpenWrite(_runtimeVfs.GetPath(_coreSettings.AssetsPackPath));
        var binaryWriter =
            new BinaryWriter(File.OpenWrite(_runtimeVfs.GetPath(_coreSettings.AssetsPackPath) + ".lookup"));
        while (assetsStream.Position < assetsStream.Length)
        {
            var asset = Query.GetUnknownAsset(assetsStream);
            if (asset == null) break;
            runtimeSerializer.Write(Query.GetUnknownAsset(assetsStream), outAssetsStream);
            IdLookupSerializer.Write(binaryWriter, asset.Id, (uint)outAssetsStream.Position);
        }
        
        //Spaces
        await using var outGlobalSpaceStream = File.OpenWrite(_runtimeVfs.GetPath(_coreSettings.GlobalSpacePath));
        SerializeSpace(_editorVfs.GetPath(_coreSettings.GlobalSpacePath), 
            _runtimeVfs.GetPath(_coreSettings.GlobalSpacePath),
            runtimeSerializer, outGlobalSpaceStream);
        
        await using var outAssetSpaceStream = File.OpenWrite(_runtimeVfs.GetPath(_coreSettings.AssetsSpacePath));
        await using var inAssetSpaceStream = File.OpenRead(_editorVfs.GetPath(_coreSettings.AssetsSpacePath));
        while (inAssetSpaceStream.Position < inAssetSpaceStream.Length)
        {
            var entity = Query.Serializer.Read<Entity>(inAssetSpaceStream);
            runtimeSerializer.Write(entity, outAssetSpaceStream);
            entity.Dispose();
        }

        var spaceDir = _runtimeVfs.GetPath(_coreSettings.LocalSpacesDirectory);
        Directory.CreateDirectory(spaceDir);
        var paths = Query.GetPaths();
        foreach (var spacePath in paths)
        {
            var path = Path.Combine(spaceDir + spacePath);
            await using var outSpaceStream = File.OpenWrite(path);
            SerializeSpace(spacePath, path, runtimeSerializer, outSpaceStream);
        }
        
        //TODO: gen settings
    }

    private void SerializeSpace(string inPath, string outPath, Serializer serializer, FileStream outStream)
    {
        var binaryWriter =
            new BinaryWriter(File.OpenWrite( outPath + ".lookup"));
        var space = Command.LoadSpace(inPath, false);
        serializer.Write(space, outStream);
        IdLookupSerializer.Write(binaryWriter, space.Id, (uint)outStream.Position);
        Command.UnloadSpace(space);
    }

    private async Task PublishProject()
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "publish " +
                        $"\"{BuildConfig.GameProjectPath}\" " +
                        "-c Release " +
                        $"-o \"{_buildSettings.OutputDirectory}\" " +
                        $"-p:AssemblyName=\"{_buildSettings.GameName}\" " +
                        $"-p:ApplicationIcon=\"{_buildSettings.IconPath}\" " +
                        $"-p:Authors=\"{_buildSettings.Company}\" " +
                        $"-p:Version={_buildSettings.Version} " +
                        $"-r {_buildSettings.RuntimeIdentifier} " +
                        $"--self-contained={_buildSettings.SelfContained.ToString().ToLower()} " +
                        $"-p:PublishSingleFile={_buildSettings.SingleFile.ToString().ToLower()} " +
                        $"-p:PublishReadyToRun={_buildSettings.ReadyToRun.ToString().ToLower()} " +
                        $"-p:PublishTrimmed={_buildSettings.Trimmed.ToString().ToLower()} " +
                        (_buildSettings.DeleteExisting ? "--force " : "") +
                        $"-p:SerializerName=\"{BuildConfig.SerializerName.Replace(",", "%2C")}\""
        };
        Console.WriteLine(processStartInfo.Arguments);
        using var process = new Process();
        process.StartInfo = processStartInfo;
        await Task.Run(process.Start);
    }
}