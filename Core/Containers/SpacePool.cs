using System.Runtime.InteropServices;
using Orbitality.Interfaces;
using Orbitality.World;

namespace Orbitality.Containers;

public class SpacePool
{
    private string[] _localSpacesPaths;
    private readonly List<Space> _localSpaces = [];
    private readonly List<SpaceFolder> _spaceFolders = [];
    public WorldSettings WorldSettings => Engine.Context.SettingsSystem.ReadSettings<WorldSettings>();

    #region SpaceFolders

    public void RegisterSpaceFolder(SpaceFolder folder)
    {
        _spaceFolders.Add(folder);
    }
    
    public SpaceFolder GetSpaceFolderWith(Guid guid)
    {
        return _spaceFolders.Find(x=>x.Guid == guid);
    }
    
    public IEnumerable<SpaceFolder> GetSpaceFoldersWith(List<Guid> guids)
    {
        return _spaceFolders.Where(x=>guids.Contains(x.Guid));
    }

    #endregion
    
    #region Spaces

    public void SetPaths(string localSpacesFolder)
    {
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
    
    public Space GetSpace(Guid guid)
    {
        return _localSpaces.First(x => x.Id == guid);
    }

    public Space LoadSpace(string path)
    {
        var space = new Space(Path.GetFileNameWithoutExtension(path), path);
        Console.WriteLine($"Loading space: {space.Name}");

        space.Initializer.Pool = BinarySpaceSaver.LoadSpace(space, path);
        
        Engine.Context.Injector.BuildDependencies(space);
        return space;
    }

    public void LoadSavedSpaces()
    {
        foreach (var space in _localSpaces.Where(space => WorldSettings.SpaceIds.Contains(space.Id)))
        {
            LoadSavedSpaces();
        }
    }

    public void SaveSpace(Space space)
    {
        BinarySpaceSaver.SaveSpace(space,Engine.GetEntitiesIn(space).ToArray());
    }
    
    #endregion

    #region Update

    public void InjectPoolsInUpdateRunners(ReadOnlySpan<IUpdateRunner> runners)
    {
        foreach (var runner in runners)
        {
            List<IComponentPool> allContainers = [];
            foreach (var space in _localSpaces.Concat([Engine.GlobalSpace]))
            {
                allContainers.AddRange(Engine.Context.EntityPool.GetComponentPoolsInSpace(space));
            }
            runner.InjectPools(CollectionsMarshal.AsSpan(allContainers));
        }
    }

    #endregion
}