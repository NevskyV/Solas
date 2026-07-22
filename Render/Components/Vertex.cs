using System.Numerics;

namespace Solas.Render.Components;

public readonly record struct Vertex(Vector3 Pos, Vector3 Color, Vector2 TexCoord)
{
    public readonly Vector3 Pos = Pos;
    public readonly Vector3 Color = Color;
    public readonly Vector2 TexCoord = TexCoord;
}