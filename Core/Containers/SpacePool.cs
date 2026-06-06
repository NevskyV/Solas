using System.Runtime.InteropServices;
using Solas.Interfaces;
using Solas.Serialization;
using Solas.Settings;
using Solas.World;

namespace Solas.Containers;

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
    
    public SpaceFolder GetSpaceFolderWith(Guid guid, Space space)
    {
        return _spaceFolders[space].Find(x=>x.Id == guid);
    }
    
    public SpaceFolder GetSpaceFolderWith(Guid guid, Guid spaceId)
    {
        return _spaceFolders[GetSpace(spaceId)].Find(x=>x.Id == guid);
    }
    
    public IEnumerable<SpaceFolder> GetSpaceFoldersWith(List<Guid> guids, Space space)
    {
        return _spaceFolders[space].Where(x=>guids.Contains(x.Id));
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
        return Engine.GlobalSpace.Id == guid? Engine.GlobalSpace : _localSpaces.FirstOrDefault(x => x.Id == guid);
    }

    public Space LoadLocalSpace(string path, Space rootSpace = null)
    {
        var space = LoadSpace(path);
        _localSpaces.Add(space);
        SpaceTree.Attach(space, rootSpace??Engine.GlobalSpace);
        return space;
    }

    public Space LoadSpace(string path, bool immediateBuild = true)
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
            space.Read(reader);
        }
        Console.WriteLine(space.Id);
        Console.WriteLine($"Loading space: {space.Name}");
        if(immediateBuild)
            Engine.Context.DISystem.BuildDependencies(space);
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
                _localSpaces.Add(LoadSpace(path, false));
            }
        }

        foreach (var space in _localSpaces)
        {
            Engine.Context.DISystem.BuildDependencies(space);
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
    
    public void SaveSpace(Space space)
    {
        using var stream = File.Open(space.Path, FileMode.OpenOrCreate, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        space.Write(writer);
    }

    public bool IsLoaded(Guid id)
    {
        return _localSpaces.Exists(x => x.Id == id) || Engine.GlobalSpace.Id == id;
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