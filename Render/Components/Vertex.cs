using System.Numerics;

namespace Solas.Render.Components;

public readonly struct Vertex(Vector2 pos, Vector3 color)
{
    public Vector2 Pos { get; init; } = pos;
    public Vector3 Color { get; init; } = color;
}