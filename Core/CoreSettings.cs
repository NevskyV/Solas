using Orbitality.Attributes;
using Orbitality.Components;

namespace Orbitality;

[SettingsSection(nameof(CoreSettings))]
public partial struct CoreSettings : IData
{
    public string GlobalSpacePath;
    public string LocalSpacesFolderPath;
    public string[] UpdateSystems;
}