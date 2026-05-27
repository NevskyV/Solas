using Orbitality.Attributes;
using Orbitality.Components;

namespace Orbitality.World;

[SettingsSection]
public partial struct WorldSettings() : IData
{
    public Guid[] SpaceIds = [];
}