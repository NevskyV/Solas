using Solas.Attributes;
using Solas.Components;

namespace Solas.Settings;

[SettingsSection]
public partial class CoreSettings : IData
{
    public string AssetsPackPath = Directory.GetCurrentDirectory() + @"\Solas\Assets.pack";
    public string AssetsSpacePath = Directory.GetCurrentDirectory() + @"\Solas\Assets.space";
    public string GlobalSpacePath = Directory.GetCurrentDirectory() + @"\Solas\Global.space";
    public string LocalSpacesDirectory = Directory.GetCurrentDirectory() + @"\Assets\";
    public float TargetFrameTime = 60.0f;
    public string[] UpdateSystems = [];
}