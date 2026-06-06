using System.Numerics;
using System.Runtime.InteropServices;
using Solas.ComponentUtils;
using Solas.Components;

namespace Solas.Transform;

public partial struct TransformData() : IData
{
    public DataProperty<Vector3> Position;
    public DataProperty<Vector3> Rotation;
    public DataProperty<Vector3> Scale;
}