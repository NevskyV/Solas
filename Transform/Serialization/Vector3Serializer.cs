using System.Numerics;
using Solas.Serialization.Core;

namespace Solas.Transform.Serialization;

public class Vector3Serializer : ICustomSerializer<Vector3>
{
    public void Write(Vector3 value, FileStream stream, Serializer serializer, string name)
    {
        serializer.Write(value.X, stream);
        serializer.Write(value.Y, stream);
        serializer.Write(value.Z, stream);
    }

    public Vector3 Read(FileStream stream)
    {
        return new Vector3(Query.Serializer.ReadFloat(stream), Query.Serializer.ReadFloat(stream), Query.Serializer.ReadFloat(stream));
    }
}