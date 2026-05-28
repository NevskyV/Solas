using Orbitality.Attributes;
using Orbitality.Components;

namespace Orbitality.Settings;

[SettingsSection]
public partial struct WorldSettings() : IData
{
    public Guid[] SpaceIds = [];
}