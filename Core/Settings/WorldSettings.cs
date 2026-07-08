using Solas.Attributes;
using Solas.Components;

namespace Solas.Settings;

[SettingsSection]
public class WorldSettings : IData
{
    public Entity Entity { get; set; }
    
    public string[] Spaces = [];
}