using Solas.Components;
using Solas.Interfaces;
using Solas.World;

namespace Solas.Systems;

internal class DISystem
{
    private readonly Dictionary<Space, List<Logic>> _cache = [];
    private readonly Dictionary<IInjectable, (Guid, Guid)[]> _injectables = [];

    internal void AddInjectable(IInjectable injectable, (Guid, Guid)[] guids)
    {
        _injectables.Add(injectable, guids);
    }

    internal void BuildDependencies(Space space)
    {
        foreach (var (obj, guids) in _injectables)
            obj.Inject(guids);
        _injectables.Clear();
    }

    internal T AutoInject<T>(Space space) where T : Logic
    {
        T result;
        if (_cache.TryGetValue(space, out var logics))
        {
            result = (T)logics.FirstOrDefault(x => x.GetType() == typeof(T));
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

    internal T Inject<T>(Guid id, Guid spaceId) where T : class, IReferenceable, new() => 
        T.SearchReferenceable<T>(id, spaceId) as T;
}