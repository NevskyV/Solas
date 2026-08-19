namespace Solas.Render.Components;

public enum MaterialPassPhase
{
    ObjectLocal,
    GlobalOverlay
}

public struct MaterialPass
{
    public CullMode CullMode;
    public bool DepthWrite;
    public MaterialPassPhase Phase;
}