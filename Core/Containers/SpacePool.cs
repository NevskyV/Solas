using System.Runtime.InteropServices;
using Orbitality.Interfaces;
using Orbitality.Serialization;
using Orbitality.Settings;
using Orbitality.Systems;
using Orbitality.World;

namespace Orbitality.Containers;

public class SpacePool
{
    private string[] _localSpacesPaths;
    private readonly List<Space> _localSpaces = [];
    private readonly Dictionary<Space,List<SpaceFolder>> _spaceFolders = [];
    public WorldSettings WorldSettings => Engine.Context.SettingsSystem.ReadSettings<WorldSettings>();

    #region SpaceFolders

    public void RegisterSpaceFolder(SpaceFolder folder, Space space)
    {
        if(!_spaceFolders.ContainsKey(space))
            _spaceFolders.Add(space, []);
        _spaceFolders[space].Add(folder);
    }
    
    public SpaceFolder GetSpaceFolderWith(Guid guid)
    {
        return _spaceFolders.Values.Select(x=>x.Find(y=>y.Id == guid)).FirstOrDefault();
    }
    
    public IEnumerable<SpaceFolder> GetSpaceFoldersWith(List<Guid> guids)
    {
        return _spaceFolders.Values.Select(x => x.Find(y => guids.Contains(y.Id)));
    }

    public List<SpaceFolder> GetAllSpaceFoldersIn(Space space)
    {
        return _spaceFolders.TryGetValue(space, out var folders) ? folders : [];
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
        Space space;
        if (!File.Exists(path) || File.ReadAllBytes(path).Length == 0)
        {
            space = new Space(Path.GetFileNameWithoutExtension(path), path, Guid.NewGuid())
            {
                Initializer =
                {
                    Pool = new InitializationPool()
                }
            };
        }
        else
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            space = new Space(Path.GetFileNameWithoutExtension(path), path, new Guid(reader.ReadBytes(16)));
            space.Initializer.Pool = BinarySpaceSaver.LoadSpace(space, path);
        }
        Console.WriteLine($"Loading space: {space.Name}");
        Engine.Context.InjectionSystem.BuildDependencies(space);
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
        if(_localSpaces.Contains(space))
            _localSpaces.Remove(space);
        SpaceTree.Detach(space);
        Engine.Context.Destroyer.DestroyIn(space);
        Engine.Context.EntityPool.UnregisterSpace(space);
    }
    
    public void UnloadAllSpaces()
    {
        var count = _localSpaces.Count;
        for (var i = 0; i < count; i++)
        {
            UnloadSpace(_localSpaces[i]);
        }
        UnloadSpace(Engine.GlobalSpace);
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