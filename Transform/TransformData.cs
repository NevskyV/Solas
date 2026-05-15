using System.Numerics;
using Orbitality.Components;
using Orbitality.ComponentUtils;

namespace Orbitality.Transform;

public class TransformData : IData
{
    public DataField<Vector3> Position;
    public DataField<Vector3> Rotation;
    public DataField<Vector3> Scale;
}