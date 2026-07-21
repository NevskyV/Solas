using Solas.Components;

namespace Solas.Render.Data;

public class CameraData : IData
{
    public Entity Entity { get; set; }
    public float FieldOfView = 60;
}