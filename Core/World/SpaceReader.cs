using Core.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Core.World;

public static class SpaceReader
{
    private const string GlobalSpacePath = "";
    
    public static Space GetGlobalSpace()
    {
        return GetSpace(GlobalSpacePath);
    }
    
    public static Space GetSpace(string path)
    {
        if (!File.Exists(GlobalSpacePath)) return new Space(new ServiceCollection(), new List<Entity>());
        
        return new Space(new ServiceCollection(), new List<Entity>());
    }
}