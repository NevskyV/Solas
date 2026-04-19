using Core.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Core.World;

public static class SpaceReader
{
    private const string GlobalSpacePath = @"";
    
    public static Space GetGlobalSpace()
    {
        return GetSpace(GlobalSpacePath);
    }

    public static Space GetLocalSpace(string path)
    {
        var space = GetSpace(path);
        Engine.WorldContext.LocalSpaces.Add(space);
        return space;
    }
    
    public static Space GetSpace(string path)
    {
        if (!File.Exists(path)) return new Space(new ServiceCollection(), new List<Entity>());

        return new Space(new ServiceCollection(), new List<Entity>());
    }
}