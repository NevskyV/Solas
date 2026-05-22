using System.Numerics;
using Orbitality.Components;
using Orbitality.ComponentUtils;

namespace Orbitality.Transform;

public class TransformData : IData
{
    public DataProperty<Vector3> Position;
    public DataProperty<Vector3> Rotation;
    public DataProperty<Vector3> Scale;
}