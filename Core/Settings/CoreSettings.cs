using Solas.Attributes;
using Solas.Components;

namespace Solas.Settings;

[SettingsSection]
public partial struct CoreSettings() : IData
{
    public string AssetsDirectory = Directory.GetCurrentDirectory() + @"\Assets\";
    public string AssetsPackPath = Directory.GetCurrentDirectory() + @"\Solas\Assets.pack";
    public string GlobalSpacePath = Directory.GetCurrentDirectory() + @"\Solas\Global.space";
    public string AssetsSpacePath = Directory.GetCurrentDirectory() + @"\Solas\Assets.space";
    public string LocalSpacesDirectory = Directory.GetCurrentDirectory() + @"\Assets\";
    public string[] UpdateSystems = [];
}