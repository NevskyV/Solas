using Solas.Attributes;
using Solas.Components;

namespace Solas.Settings;

[SettingsSection]
public class BuildSettings : IData
{
    public string Serializer = "Solas.Serialization.Binary.BinarySerializer, Core";
    
    public string GameName = "My Game";
    public string IconPath = "";
    public string Company ="Default Company";
    public string Version = "1.0.0";
    public string OutputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "publish");
    public string RuntimeIdentifier = "win-x64";
    public bool SelfContained = true;
    public bool SingleFile = true;
    public bool ReadyToRun = false;
    public bool Trimmed = false;
    public bool DeleteExisting = true;
}