using System.Numerics;
using Solas.Attributes;
using Solas.Components;

namespace Solas.Render.Settings;

[SettingsSection]
public class GraphicsSettings : IData
{
    public Entity Entity { get; set; }

    #region Image smoothness

    public ushort VsyncMode = 1;
    public ushort MaxFramesInFlight = 2;

    #endregion

    #region Quality

    public float RenderScale = 1;
    public ushort Msaa = 2;
    public float AnisotropyLevel = 16.0f;
    public bool SupportsHdr = false;
    public Vector3 TileSize = new(16, 16, 16);
    public uint ShadowMapResolution = 3072;
    public uint ShadowCascadeCount = 4;
    public float ShadowSplitLambda = 0.7f;
    public float ShadowMaxDistance = 120.0f;
    public float ShadowSpotPaddingDegrees = 4.0f;
    public uint MaxShadowedLights = 2;

    #endregion

    #region Debug

    public bool EnableValidationLayers = true;
    public ushort PolygonMode = 0;

    #endregion
}