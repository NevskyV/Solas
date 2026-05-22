using Orbitality.Attributes;
namespace Orbitality;

[SettingsSection(nameof(CoreSettings))]
public struct CoreSettings
{
    public string GlobalSpacePath;
    public string LocalSpacesFolderPath;
    public Type[] UpdateSystems;
}
