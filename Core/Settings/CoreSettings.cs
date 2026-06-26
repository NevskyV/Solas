using Solas.Attributes;
using Solas.Components;

namespace Solas.Settings;

[SettingsSection]
public class CoreSettings : IData
{
    public string AssetsPackPath = "assets://Assets.pack";
    public string AssetsSpacePath = "assets://Assets.space";
    public string GlobalSpacePath = "assets://Global.space";
    public string LocalSpacesDirectory = "assets://";
    public float TargetFrameTime = 60.0f;
    public string[] UpdateSystems = [];
}