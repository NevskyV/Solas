using Solas.Components;
using Solas.Interfaces;
using Solas.World;

namespace Solas.Systems;

internal class DISystem
{
    private readonly Dictionary<Space, List<Logic>> _cache = [];
    private readonly Dictionary<Space,Dictionary<IInjectable, (Guid, Guid)[]>> _injectables = [];

    internal void AddInjectable(IInjectable injectable, (Guid, Guid)[] guids, Space space)
    {
        if (_injectables.TryGetValue(space, out var inSpace))
        {
            inSpace.Add(injectable, guids);
        }
        else
        {
            _injectables.Add(space, new Dictionary<IInjectable, (Guid, Guid)[]>{{injectable, guids}});
        }
    }
    
    internal void BuildDependencies(Space space)
    {
        if (!_injectables.TryGetValue(space, out var inSpace)) return;
        foreach (var (obj, guids) in inSpace)
        {
            obj.Inject(guids);
        }
        _injectables.Clear();
    }

    internal T AutoInject<T>(Space space) where T : Logic
    {
        T result;
        if (_cache.TryGetValue(space, out var logics))
        {
            result = (T)logics.FirstOrDefault(x=>x.GetType() == typeof(T));
            if (result == null)
            {
                result = EngineContext.EntityPool.GetComponentByTypeInAvailable<T>(space);
                _cache[space].Add(result);
            }
        }
        else
        {
            result = EngineContext.EntityPool.GetComponentByTypeInAvailable<T>(space);
            _cache.Add(space, [result]);
        }

        return result;
    }

    internal T Inject<T>(Guid id, Guid spaceId) where T : class, IReferenceable, new()
    {
        if (EngineContext.SpacePool.IsLoaded(spaceId))
        {
            return EngineContext.SpacePool.GetSpaceFolderWith(id, spaceId) as T;
        }
        return EngineContext.AssetsPool.LoadAsset<T>(id);
    }
    
    internal Entity Inject(Guid id, Guid spaceId)
    {
        var loadedSpace = EngineContext.SpacePool.GetSpace(spaceId);
        if (loadedSpace != null)
        {
            return EngineContext.EntityPool.GetEntitiesIn(loadedSpace).First(x=>x.Id==id); 
        }

        return EngineContext.AssetsPool.LoadEntity(id);
    }
}