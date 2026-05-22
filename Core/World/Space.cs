using Microsoft.Extensions.DependencyInjection;
using Orbitality.Containers;
using Orbitality.Systems;

namespace Orbitality.World;

public class Space
{
    public string Name { get; init; }
    public string Path { get; init; }
    public readonly Initializer Initializer;

    public Space(string name, string path)
    {
        Name = name;
        Path = path;
        Initializer = new Initializer(this);
        Engine.Context.EntityPool.RegisterSpace(this);
        Engine.Context.Destroyer.AddSpace(this);
    }
}