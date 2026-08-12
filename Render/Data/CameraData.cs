using System.Numerics;
using Solas.Attributes;
using Solas.Components;
using Solas.Render.Components;

namespace Solas.Render.Data;

public partial class CameraData : IData
{
    public Entity Entity { get; set; }
    public CameraType Type;
    public float FieldOfView = 60;
    public float Size = 10;
    public float NearClipPlane = 0.1f;
    public float FarClipPlane = 1000f;
    public Vector3 BackgroundColor = new(0.1f, 0.1f, 0.15f);
    [SerializationIgnore] public Material? ScreenMaterial;
}