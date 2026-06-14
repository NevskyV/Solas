using Solas.Attributes;
using Solas.Components;

namespace Solas.Settings;

[SettingsSection]
public class WorldSettings : IData
{
    public string[] Spaces = [];
}