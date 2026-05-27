using Orbitality.Attributes;
using Orbitality.Components;

namespace Orbitality;

[SettingsSection]
public partial struct CoreSettings() : IData
{
    public string GlobalSpacePath = Directory.GetCurrentDirectory() + @"\Orbitality\Global.space";
    public string LocalSpacesFolderPath = Directory.GetCurrentDirectory() + @"\Assets";
    public string[] UpdateSystems = [];
}