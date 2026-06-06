using System.Numerics;
using Solas.ComponentUtils;
using Solas.Components;

namespace Solas.Transform;

public sealed partial class TransformData : IData
{
    public DataProperty<Vector3> Position;
    public DataProperty<Vector3> Rotation;
    public DataProperty<Vector3> Scale;
}