using System.Numerics;

namespace Solas.Render.Components;

public struct Vertex(Vector2 pos, Vector3 color)
{
    public Vector2 Pos = pos;
    public Vector3 Color = color;
}