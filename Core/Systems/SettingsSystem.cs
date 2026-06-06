using Solas.Components;
using Solas.Serialization;

namespace Solas.Systems;

public class SettingsSystem
{
    private readonly Dictionary<Type, string> _settingsPaths = new();
    
    public IData[] ReadAllSettings(string pathToSettingsFolder)
    {
        var files = Directory.GetFiles(pathToSettingsFolder,  "*.set");
        IData[] settings = new IData[files.Length];
        for(var i = 0; i < files.Length; i++)
        {
            var stream = File.Open(files[i], FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);
            
            var typeName = reader.ReadString();
            var data = DataSerializationRegistry.Read(typeName, reader, out _);
            settings[i] = data;
            
            _settingsPaths.TryAdd(data.GetType(), files[i]);
        }
        
        return settings;
    }
    
    public void WriteSettings(IData settings)
    {
        var type = settings.GetType();
        
        using var stream = File.Open(_settingsPaths[type], FileMode.OpenOrCreate, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        
        writer.Write($"{type.FullName}, {type.Assembly.GetName().Name}");
        DataSerializationRegistry.Write(writer, settings, null);
    }
    
    public void WriteNewSettings(IData settings, string path)
    {
        _settingsPaths.Add(settings.GetType(), path);
        WriteSettings(settings);
    }

    public T ReadSettings<T>() where T : struct, IData
    {
        return (T)Engine.SettingsContext.First(x => x.GetType().IsAssignableTo(typeof(T)));
    }
}