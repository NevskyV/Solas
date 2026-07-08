using System.Numerics;
using Solas.Serialization.Core;

namespace Solas.Transform.Serialization;

public class Vector2Serializer : ICustomSerializer<Vector2>
{
    public void Write(Vector2 value, FileStream stream, Serializer serializer, string name)
    {
        serializer.Write(value.X, stream);
        serializer.Write(value.Y, stream);
    }

    public Vector2 Read(FileStream stream)
    {
        return new Vector2(Query.Serializer.ReadFloat(stream), Query.Serializer.ReadFloat(stream));
    }
}