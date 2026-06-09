using Solas.Settings;
using Solas.World;

namespace Solas;

public static class WorldContext
{
    public static Space GlobalSpace
    {
        get;
        internal set => field ??= value;
    }

    public static CoreSettings CoreSettings
    {
        get;
        internal set => field ??= value;
    }
}