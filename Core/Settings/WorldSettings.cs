using Solas.Attributes;
using Solas.Components;

namespace Solas.Settings;

[SettingsSection]
public partial struct WorldSettings() : IData
{
    public Guid[] SpaceIds = [];
}