using Solas.Assets;
using StbImageSharp;

namespace Solas.Render.Components;

public class Texture // : Asset
{
    public byte[]? Data { get; private init; }
    public uint Width { get; private init; }
    public uint Height { get; private init; }

    public Texture(string path)
    {
        using var stream = File.OpenRead(path);
        var result = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        Data = result.Data;
        Width = (uint)result.Width;
        Height = (uint)result.Height;
    }
}