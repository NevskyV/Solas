using Solas.Components;

namespace Solas.Render.Data;

public class CameraData : IData
{
    public Entity Entity { get; set; }
    public float FieldOfView = 60;
    public float NearClipPlane = 0.1f;
    public float FarClipPlane = 1000f;
}