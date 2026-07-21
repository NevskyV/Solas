using System.Numerics;

namespace Solas.Render.Components;

public readonly struct Vertex(Vector3 pos, Vector3 color, Vector2 texCoord)
{
    public readonly Vector3 Pos = pos;
    public readonly Vector3 Color = color;
    public readonly Vector2 TexCoord = texCoord;
}