using Orbitality.Components;

namespace Orbitality.Systems;

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
            var type = Type.GetType(typeName)!;
            var method = type.GetMethod("Read")!;
            var data = method.Invoke(null, [reader]);
            settings[i] = (IData)data;
            
            _settingsPaths.TryAdd(type, files[i]);
        }
        
        return settings;
    }
    
    public void WriteSettings(IData settings)
    {
        var type = settings.GetType();
        
        using var stream = File.Open(_settingsPaths[type], FileMode.OpenOrCreate, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        
        writer.Write($"{type.FullName}, {type.Assembly.GetName().Name}");

        var method = type.GetMethod("Write")!;
        method.Invoke(settings, [writer, settings]);
    }
    
    public void WriteNewSettings(IData settings, string path)
    {
        _settingsPaths.Add(settings.GetType(), path);
        WriteSettings(settings);
    }
}