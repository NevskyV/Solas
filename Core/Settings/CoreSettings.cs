using Solas.Attributes;
using Solas.Components;

namespace Solas.Settings;

[SettingsSection]
public partial struct CoreSettings() : IData
{
    public string GlobalSpacePath = Directory.GetCurrentDirectory() + @"\Orbitality\Global.space";
    public string LocalSpacesFolderPath = Directory.GetCurrentDirectory() + @"\Assets";
    public string[] UpdateSystems = [];
}