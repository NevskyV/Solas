using Solas.Components;
using Solas.Interfaces;
using Solas.World;

namespace Solas.Systems;

internal class DISystem
{
    private readonly Dictionary<Space, List<Logic>> _cache = [];
    private readonly List<(IInjectable, (Guid, Guid)[])> _injectables = [];
    private readonly Queue<(Guid, Guid)[]> _lastInjectables = [];
    internal ReadOnlySpan<(Guid, Guid)> LastInjectables => _lastInjectables.TryDequeue(out var result) ? result : [];

    internal void AddInjectable(IInjectable injectable, (Guid, Guid)[] guids)
    {
        if (guids is not { Length: > 0 }) return;
        _lastInjectables.Enqueue(guids);
        _injectables.Add((injectable, guids));
    }

    internal void BuildDependencies(Space space)
    {
        foreach (var (obj, guids) in _injectables)
        {
            obj.Inject(guids);
        }

        _injectables.Clear();
    }

    internal T AutoInject<T>(Space space) where T : Logic
    {
        T result = null;
        var targetType = typeof(T);

        if (_cache.TryGetValue(space, out var logics))
        {
            for (int i = 0; i < logics.Count; i++)
            {
                if (logics[i].GetType() == targetType)
                {
                    result = (T)logics[i];
                    break;
                }
            }

            if (result == null)
            {
                result = EngineContext.EntityPool.GetComponentByTypeInAvailable<T>(space);
                if (result != null)
                    logics.Add(result);
            }
        }
        else
        {
            result = EngineContext.EntityPool.GetComponentByTypeInAvailable<T>(space);
            if (result != null)
                _cache.Add(space, [result]);
        }

        return result;
    }

    internal static T Inject<T>(Guid id, Guid spaceId) where T : class, IReferenceable
    {
        return T.SearchReferenceable<T>(id, spaceId) as T;
    }
}