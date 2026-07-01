using System.Diagnostics;
using Solas.Components;
using Solas.Registries;
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
        var editorSerializer = Query.Serializer;
        var runtimeSerializer = (Serializer)Activator.CreateInstance(Type.GetType(_buildSettings.Serializer)!);

        if (editorSerializer == null || runtimeSerializer == null) return;

        //Assets
        await using var assetsStream = File.OpenRead(_editorVfs.GetPath(_coreSettings.AssetsPackPath));
        await using var outAssetsStream = File.OpenWrite(_runtimeVfs.GetPath(_coreSettings.AssetsPackPath));
        await using var binaryWriter =
            new BinaryWriter(File.Open(_runtimeVfs.GetPath(_coreSettings.AssetsPackPath) + ".lookup", FileMode.Append,
                FileAccess.Write));

        editorSerializer.Open(assetsStream);
        runtimeSerializer.Open(outAssetsStream);
        while (true)
        {
            var asset = Query.GetUnknownAsset(assetsStream);

            if (asset == null) break;
            Engine.UpdateSerializer(runtimeSerializer);
            Command.WriteAsset(asset, outAssetsStream, binaryWriter);
            Engine.UpdateSerializer(editorSerializer);
        }

        editorSerializer.Close(assetsStream);
        runtimeSerializer.Close(outAssetsStream);

        //Spaces
        await using var outGlobalSpaceStream = File.OpenWrite(_runtimeVfs.GetPath(_coreSettings.GlobalSpacePath));
        SerializeSpace(_editorVfs.GetPath(_coreSettings.GlobalSpacePath),
            editorSerializer, runtimeSerializer, outGlobalSpaceStream);

        await using var outAssetSpaceStream = File.OpenWrite(_runtimeVfs.GetPath(_coreSettings.AssetsSpacePath));
        await using var inAssetSpaceStream = File.OpenRead(_editorVfs.GetPath(_coreSettings.AssetsSpacePath));
        await using var assetSpaceBinaryWriter = new BinaryWriter(
            File.Open(_runtimeVfs.GetPath(_coreSettings.AssetsSpacePath) + ".lookup", FileMode.Append,
                FileAccess.Write));

        while (true)
        {
            try
            {
                var entity = Query.Serializer.Read<Entity>(inAssetSpaceStream);
                runtimeSerializer.Write(entity, outAssetSpaceStream);
                IdLookupSerializer.Write(assetSpaceBinaryWriter, entity.Id,
                    (uint)assetSpaceBinaryWriter.BaseStream.Position);
                entity.Dispose();
            }
            catch (Exception)
            {
                //stop iterating
                break;
            }
        }

        var spaceDir = _runtimeVfs.GetPath(_coreSettings.LocalSpacesDirectory);
        Directory.CreateDirectory(spaceDir);
        var paths = Query.GetPaths();
        foreach (var spacePath in paths)
        {
            var path = Path.Combine(spaceDir + spacePath);
            await using var outSpaceStream = File.OpenWrite(path);
            SerializeSpace(spacePath, editorSerializer, runtimeSerializer, outSpaceStream);
        }

        Engine.UpdateSerializer(runtimeSerializer);
        Engine.SetVfs(_runtimeVfs);
        new SettingsFilesRegistry().CreateAll();
    }

    private void SerializeSpace(string inPath, Serializer editorSerializer, Serializer runtimeSerializer,
        FileStream outStream)
    {
        var space = Command.LoadSpace(inPath, false);
        Engine.UpdateSerializer(runtimeSerializer);

        runtimeSerializer.Write(space, outStream);

        Command.UnloadSpace(space);

        Engine.UpdateSerializer(editorSerializer);
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
                        //$"-p:TargetName=\"{_buildSettings.GameName}\" " +
                        $"-p:ApplicationIcon=\"{_buildSettings.IconPath}\" " +
                        $"-p:Authors=\"{_buildSettings.Company}\" " +
                        $"-p:Version={_buildSettings.Version} " +
                        $"-r {_buildSettings.RuntimeIdentifier} " +
                        $"--self-contained={_buildSettings.SelfContained.ToString().ToLower()} " +
                        $"-p:PublishSingleFile={_buildSettings.SingleFile.ToString().ToLower()} " +
                        $"-p:PublishReadyToRun={_buildSettings.ReadyToRun.ToString().ToLower()} " +
                        $"-p:PublishTrimmed={_buildSettings.Trimmed.ToString().ToLower()} " +
                        (_buildSettings.DeleteExisting ? "--force " : "") +
                        $"-p:SerializerName=\"{_buildSettings.Serializer.Replace(",", "%2C")}\""
        };
        Console.WriteLine(processStartInfo.Arguments);
        using var process = new Process();
        process.StartInfo = processStartInfo;
        await Task.Run(process.Start);
    }
}