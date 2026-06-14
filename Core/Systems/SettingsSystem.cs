using Solas.Components;
using Solas.Generated;
using Solas.Registries;
using Solas.Serialization;
using Solas.Serialization.Core;

namespace Solas.Systems;

internal class SettingsSystem
{
    private readonly Dictionary<Type, IData> _settings = [];
    private readonly Dictionary<Type, string> _settingsPaths = new();

    internal void ReadAllSettings(string pathToSettingsFolder)
    {
        SettingsFileGenerator.CreateFiles();
        var files = Directory.GetFiles(pathToSettingsFolder, "*.set");
        foreach (var path in files)
        {
            var stream = File.Open(path, FileMode.Open, FileAccess.Read);
            EngineContext.Serializer.Open(stream);
            var typeName = EngineContext.Serializer.ReadString(stream);
            var data = EngineContext.DataReadingRegistry.Read(typeName, stream);
            _settings.Add(data.GetType(), data);
            EngineContext.Serializer.Close(stream);
            _settingsPaths.TryAdd(data.GetType(), path);
        }
    }

    internal void WriteExistingSettings(IData settings)
    {
        var type = settings.GetType();

        using var stream = File.Open(_settingsPaths[type], FileMode.OpenOrCreate, FileAccess.Write);

        EngineContext.Serializer.Write($"{type.FullName}, {type.Assembly.GetName().Name}", stream);
        EngineContext.Serializer.Write(settings, stream);
    }

    internal void WriteNewSettings(IData settings, string path)
    {
        _settingsPaths.Add(settings.GetType(), path);
        WriteExistingSettings(settings);
    }

    internal T GetSettings<T>() where T : class, IData
    {
        return (T)_settings[typeof(T)];
    }
}