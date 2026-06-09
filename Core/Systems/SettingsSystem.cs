using Solas.Components;
using Solas.Serialization;

namespace Solas.Systems;

internal class SettingsSystem
{
    private readonly Dictionary<Type, IData> _settings = [];
    private readonly Dictionary<Type, string> _settingsPaths = new();

    internal void ReadAllSettings(string pathToSettingsFolder)
    {
        var files = Directory.GetFiles(pathToSettingsFolder, "*.set");
        foreach (var path in files)
        {
            var stream = File.Open(path, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            var typeName = reader.ReadString();
            var data = DataSerializationRegistry.Read(typeName, reader, out _);
            _settings.Add(data.GetType(), data);

            _settingsPaths.TryAdd(data.GetType(), path);
        }
    }

    internal void WriteExistingSettings(IData settings)
    {
        var type = settings.GetType();

        using var stream = File.Open(_settingsPaths[type], FileMode.OpenOrCreate, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        writer.Write($"{type.FullName}, {type.Assembly.GetName().Name}");
        DataSerializationRegistry.Write(writer, settings, null);
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