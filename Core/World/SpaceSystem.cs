using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orbitality.Components;
using Orbitality.Containers;

namespace Orbitality.World;

public class SpaceSystem()
{
    private string _localSpacesFolder;
    private string[] _localSpacesPaths;
    private readonly List<Space> _localSpaces = [];

    public void SetPaths(string localSpacesFolder)
    {
        _localSpacesFolder = localSpacesFolder;
        _localSpacesPaths = Directory.GetFiles(localSpacesFolder, "*.space", SearchOption.AllDirectories);
    }

    public List<Task> InitializeLocalSpaces()
    {
        List<Task> result = [];
        foreach (var space in _localSpaces)
        {
            result.AddRange(space.Initializer.InitializeDependencies());
        }
        return result;
    }

    public Space LoadLocalSpace(string name)
    {
        var space = LoadSpace(_localSpacesPaths.First(x => Path.GetFileNameWithoutExtension(x) == name));
        _localSpaces.Add(space);
        return space;
    }

    public Space LoadSpace(string path)
    {
        var space = new Space(Path.GetFileNameWithoutExtension(path), path);
        Console.WriteLine($"Loading space: {space.Name}");

        space.Initializer.Container = BinarySpaceSaver.LoadSpace(space, path);
        
        Engine.Context.Injector.BuildDependencies(space);
        return space;
    }

    public void SaveSpace(Space space)
    {
        BinarySpaceSaver.SaveSpace(space.Initializer.Container,Engine.GetEntitiesIn(space).ToArray(), space.Path);
    }
}