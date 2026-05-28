using System.Runtime.InteropServices;
using Orbitality.Interfaces;
using Orbitality.Serialization;
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

    public IEnumerable<Task> InitializeLocalSpaces()
    {
        return _localSpaces.SelectMany(x=>x.Initializer.InitializeDependencies());
    }
    
    public Space GetSpace(Guid guid)
    {
        return _localSpaces.First(x => x.Id == guid);
    }

    public Space LoadLocalSpace(string path, Space rootSpace = null)
    {
        var space = LoadSpace(path);
        _localSpaces.Add(space);
        SpaceTree.Attach(space, rootSpace??Engine.GlobalSpace);
        return space;
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
        foreach (var path in _localSpacesPaths)
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            if(WorldSettings.SpaceIds.Contains(new Guid(reader.ReadBytes(16))))
            {
                _localSpaces.Add(LoadSpace(path));
            }
        }
        SpaceTree.Create(_localSpaces);
    }

    public void UnloadSpace(Space space)
    {
        _localSpaces.Remove(space);
        SpaceTree.Detach(space);
        Engine.Context.EntityPool.UnregisterSpace(space);
        Engine.Context.Destroyer.DestroyIn(space);
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