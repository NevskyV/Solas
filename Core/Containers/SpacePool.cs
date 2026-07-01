using System.Diagnostics;
using System.Runtime.InteropServices;
using Solas.Interfaces;
using Solas.Settings;
using Solas.World;

namespace Solas.Containers;

internal class SpacePool
{
    private readonly List<Space> _localSpaces = [];
    private readonly Dictionary<Space, List<SpaceFolder>> _spaceFolders = [];
    private string[] _localSpacesPaths = [];
    private WorldSettings WorldSettings => Query.GetSettings<WorldSettings>();

    #region Update

    internal void InjectPoolsInUpdateRunners(ReadOnlySpan<IUpdateRunner> runners)
    {
        foreach (var runner in runners)
        {
            List<IComponentPool> allContainers = [];
            foreach (var space in _localSpaces.Concat([WorldContext.GlobalSpace]))
                allContainers.AddRange(EngineContext.EntityPool.GetComponentPoolsInSpace(space));
            runner.InjectPools(CollectionsMarshal.AsSpan(allContainers));
        }
    }

    #endregion

    #region SpaceFolders

    internal void RegisterSpaceFolder(SpaceFolder folder, Space space)
    {
        if (!_spaceFolders.ContainsKey(space))
            _spaceFolders.Add(space, []);
        _spaceFolders[space].Add(folder);
    }

    internal void UnregisterSpaceFolder(SpaceFolder folder, Space space)
    {
        _spaceFolders[space].Remove(folder);
    }

    internal SpaceFolder GetSpaceFolderWith(Guid guid, Space space)
    {
        return _spaceFolders[space].FirstOrDefault(x => x.Id == guid);
    }

    internal SpaceFolder GetSpaceFolderWith(Guid guid, Guid spaceId)
    {
        return _spaceFolders[GetSpace(spaceId)].Find(x => x.Id == guid);
    }

    internal IEnumerable<SpaceFolder> GetSpaceFoldersWith(List<Guid> guids, Space space)
    {
        return _spaceFolders[space].Where(x => guids.Contains(x.Id));
    }

    internal List<SpaceFolder> GetAllSpaceFoldersIn(Space space)
    {
        return _spaceFolders.TryGetValue(space, out var folders) ? folders : [];
    }

    #endregion

    #region Spaces

    internal void SetPaths(string localSpacesFolder)
    {
        _localSpacesPaths = Directory.GetFiles(localSpacesFolder, "*.space", SearchOption.AllDirectories);
    }

    internal string[] GetPaths()
    {
        return _localSpacesPaths;
    }

    internal IEnumerable<Task> InitializeLocalSpaces()
    {
        return _localSpaces.SelectMany(x => x.Initializer.InitializeDependencies());
    }

    internal Space GetSpace(Guid guid)
    {
        return WorldContext.GlobalSpace.Id == guid
            ? WorldContext.GlobalSpace
            : _localSpaces.FirstOrDefault(x => x.Id == guid);
    }

    internal Space LoadLocalSpace(string path, Space rootSpace = null)
    {
        var space = LoadSpace(path);
        _localSpaces.Add(space);
        SpaceTree.Attach(space, rootSpace ?? WorldContext.GlobalSpace);
        return space;
    }

    internal Space LoadSpace(string path, bool immediateBuild = true)
    {
        Space space;
        if (!File.Exists(path) || File.ReadAllBytes(path).Length == 0)
        {
            space = new Space(Guid.NewGuid())
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Path = path,
                Initializer =
                {
                    Pool = new InitializationPool()
                }
            };
        }
        else
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read);

            space = EngineContext.Serializer.Read<Space>(stream);
            if (space == null) throw new NullReferenceException();
            space.Name = Path.GetFileNameWithoutExtension(path);
            space.Path = path;
        }
        
        Debug.WriteLine($"Loading space: {space.Name} with id {space.Id}");
        if (immediateBuild)
            EngineContext.DISystem.BuildDependencies(space);
        return space;
    }

    internal void LoadSavedSpaces()
    {
        foreach (var path in WorldSettings.Spaces) _localSpaces.Add(LoadSpace(path, false));

        foreach (var space in _localSpaces) EngineContext.DISystem.BuildDependencies(space);
        SpaceTree.Create(_localSpaces);
    }

    internal void UnloadSpace(Space space)
    {
        if (_localSpaces.Contains(space))
            _localSpaces.Remove(space);
        SpaceTree.Detach(space);
        EngineContext.Destroyer.DestroyIn(space);
        EngineContext.EntityPool.UnregisterSpace(space);
    }

    internal void UnloadAllSpaces()
    {
        var count = _localSpaces.Count;
        for (var i = 0; i < count; i++) UnloadSpace(_localSpaces[i]);
        UnloadSpace(WorldContext.GlobalSpace);
    }

    internal void SaveSpace(Space space)
    {
        using var stream = File.Open(space.Path, FileMode.OpenOrCreate, FileAccess.Write);
        EngineContext.Serializer.Write(space, stream);
    }

    #endregion
}