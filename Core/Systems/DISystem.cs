using Solas.Components;
using Solas.Interfaces;
using Solas.World;

namespace Solas.Systems;

public class DISystem
{
    private readonly Dictionary<Space, List<Logic>> _cache = [];
    private Dictionary<Space,Dictionary<IInjectable, (Guid, Guid)[]>> _injectables = [];

    public void AddInjectable(IInjectable injectable, (Guid, Guid)[] guids, Space space)
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
    
    public void BuildDependencies(Space space)
    {
        if (!_injectables.TryGetValue(space, out var inSpace)) return;
        foreach (var (obj, guids) in inSpace)
        {
            obj.Inject(guids);
        }
        _injectables.Clear();
    }

    public T AutoInject<T>(Space space) where T : Logic
    {
        T result;
        if (_cache.TryGetValue(space, out var logics))
        {
            result = (T)logics.FirstOrDefault(x=>x.GetType() == typeof(T));
            if (result == null)
            {
                result = Engine.Context.EntityPool.GetComponentBySingleTypeInAllAvailable<T>(space);
                _cache[space].Add(result);
            }
        }
        else
        {
            result = Engine.Context.EntityPool.GetComponentBySingleTypeInAllAvailable<T>(space);
            _cache.Add(space, [result]);
        }

        return result;
    }

    public T Inject<T>(Guid id, Guid spaceId) where T : class, IReferenceable, new()
    {
        if (Engine.Context.SpacePool.IsLoaded(spaceId))
        {
            return Engine.Context.SpacePool.GetSpaceFolderWith(id, spaceId) as T;
        }
        return Engine.Context.AssetsPool.LoadAsset<T>(id);
    }
    
    public Entity Inject(Guid id, Guid spaceId)
    {
        var loadedSpace = Engine.Context.SpacePool.GetSpace(spaceId);
        if (loadedSpace != null)
        {
            return Engine.Context.EntityPool.GetEntitiesInAllAvailable(loadedSpace).First(x=>x.Id==id); 
        }

        return Engine.Context.AssetsPool.LoadEntity(id);
    }
}