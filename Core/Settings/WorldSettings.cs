using Solas.Attributes;
using Solas.Components;

namespace Solas.Settings;

[SettingsSection]
public partial class WorldSettings() : IData
{
    public Guid[] SpaceIds = [];
}