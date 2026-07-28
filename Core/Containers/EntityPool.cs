using System.Buffers;
using Solas.Components;
using Solas.ComponentUtils;
using Solas.Interfaces;
using Solas.World;

namespace Solas.Containers;

internal class EntityPool
{
    private readonly Dictionary<Space, Dictionary<Type, IComponentPool>> _componentPoolsInSpaces = [];
    private readonly Dictionary<Space, List<Entity>> _entitiesInSpaces = [];

    internal List<IUpdateRunner> UpdateRunners { get; } = [];
    internal List<IUpdateRunner> FixedUpdateRunners { get; } = [];
    internal List<IUpdateRunner> LateUpdateRunners { get; } = [];

    #region Registration

    internal void RegisterSpace(Space space)
    {
        _entitiesInSpaces.Add(space, new List<Entity>());
        _componentPoolsInSpaces.Add(space, new Dictionary<Type, IComponentPool>());
    }

    internal void UnregisterSpace(Space space)
    {
        _entitiesInSpaces.Remove(space);
        _componentPoolsInSpaces.Remove(space);
    }

    private ComponentPool<T> RegisterPool<T>(Space space)
    {
        var type = typeof(T);
        if (_componentPoolsInSpaces[space].TryGetValue(type, out var componentPool))
            return (ComponentPool<T>)componentPool;

        var pool = new ComponentPool<T>();
        _componentPoolsInSpaces[space][type] = pool;
        return pool;
    }

    internal void RegisterEntity(Entity entity)
    {
        entity.CurrentSpace ??= _entitiesInSpaces.Keys.Last();
        _entitiesInSpaces[entity.CurrentSpace].Add(entity);

        var logics = entity.Logics;
        for (int i = 0; i < logics.Length; i++) AddReferences(logics[i], entity);

        var data = entity.Data;
        for (int i = 0; i < data.Length; i++) AddReferences(data[i], entity);
    }

    internal void UnregisterEntity(Entity entity)
    {
        _entitiesInSpaces[entity.CurrentSpace].Remove(entity);

        var folders = Query.GetAllSpaceFoldersIn(entity.CurrentSpace);
        for (int i = 0; i < folders.Count; i++)
        {
            if (folders[i].EntityIds.Contains(entity.Id))
            {
                folders[i].EntityIds.Remove(entity.Id);
                break;
            }
        }

        var logics = entity.Logics;
        for (int i = 0; i < logics.Length; i++) RemoveReferences(logics[i], entity);

        var data = entity.Data;
        for (int i = 0; i < data.Length; i++) RemoveReferences(data[i], entity);
    }

    internal void UnregisterEntityById(Space space, Guid id)
    {
        if (!_entitiesInSpaces.TryGetValue(space, out var entities)) return;

        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i].Id == id)
            {
                UnregisterEntity(entities[i]);
                break;
            }
        }
    }

    internal void RegisterRunner(IUpdateRunner runner) => UpdateRunners.Add(runner);
    internal void RegisterFixedRunner(IUpdateRunner runner) => FixedUpdateRunners.Add(runner);
    internal void RegisterLateRunner(IUpdateRunner runner) => LateUpdateRunners.Add(runner);

    internal void AddReferences<T>(T component, Entity entity)
    {
        var rawPool = RegisterPool<T>(entity.CurrentSpace);
        rawPool.Add(component, entity);
    }

    internal void RemoveReferences<T>(T _, Entity entity)
    {
        var type = typeof(T);

        if (_componentPoolsInSpaces[entity.CurrentSpace].TryGetValue(type, out var pool)) pool.Remove(entity);
    }

    #endregion

    #region Search

    internal IEnumerable<Entity> GetEntitiesIn(Space space)
    {
        return _entitiesInSpaces.TryGetValue(space, out var list) ? list : Array.Empty<Entity>();
    }

    internal IEnumerable<Entity> GetEntitiesIn(SpaceFolder spaceFolder)
    {
        if (!_entitiesInSpaces.TryGetValue(spaceFolder.Space, out var entities)) yield break;

        var ids = spaceFolder.EntityIds;
        for (int i = 0; i < entities.Count; i++)
        {
            if (ids.Contains(entities[i].Id))
                yield return entities[i];
        }
    }

    internal IEnumerable<Entity> GetEntitiesInAvailable(Space space)
    {
        var availableSpaces = SpaceTree.GetAllAvailableSpacesFor(space);
        for (int i = 0; i < availableSpaces.Count; i++)
        {
            if (_entitiesInSpaces.TryGetValue(availableSpaces[i], out var list))
            {
                for (int j = 0; j < list.Count; j++)
                    yield return list[j];
            }
        }
    }

    internal IEnumerable<Entity> GetEntitiesInAvailableWith(Space space, params Type[] types)
    {
        var availableSpaces = SpaceTree.GetAllAvailableSpacesFor(space);
        for (int i = 0; i < availableSpaces.Count; i++)
        {
            foreach (var entity in GetEntitiesWith(availableSpaces[i], types))
            {
                yield return entity;
            }
        }
    }

    internal IEnumerable<Entity> GetEntitiesWith(Space space, params Type[] types)
    {
        if (types == null || types.Length == 0) yield break;
        if (!_entitiesInSpaces.TryGetValue(space, out var entities)) yield break;

        var totalChunks = ComponentRegistry.Count / 32 + 1;

        var filter = ArrayPool<uint>.Shared.Rent(totalChunks);
        Array.Clear(filter, 0, totalChunks);

        try
        {
            for (int i = 0; i < types.Length; i++)
            {
                var id = ComponentRegistry.GetId(types[i]);
                filter[id / 32] |= 1u << (id % 32);
            }

            for (int i = 0; i < entities.Count; i++)
            {
                if (IsMatch(entities[i].MaskChunks, filter, totalChunks))
                    yield return entities[i];
            }
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(filter);
        }
    }

    private static bool IsMatch(uint[] entityMask, uint[] filter, int filterLength)
    {
        for (var i = 0; i < filterLength; i++)
        {
            var entityChunk = i < entityMask.Length ? entityMask[i] : 0u;
            if ((entityChunk & filter[i]) != filter[i]) return false;
        }

        return true;
    }

    internal IEnumerable<Entity> GetEntitiesByType<T>(Space space)
    {
        var type = typeof(T);
        if (_componentPoolsInSpaces.TryGetValue(space, out var pools) && pools.TryGetValue(type, out var value))
        {
            var pool = (ComponentPool<T>)value;
            return pool.Entities;
        }

        return [];
    }

    internal IEnumerable<Entity> GetEntitiesByTypeInAvailable<T>(Space space)
    {
        var availableSpaces = SpaceTree.GetAllAvailableSpacesFor(space);
        for (int i = 0; i < availableSpaces.Count; i++)
        {
            foreach (var entity in GetEntitiesByType<T>(availableSpaces[i]))
            {
                yield return entity;
            }
        }
    }

    internal IEnumerable<T> GetComponentsByType<T>(Space space)
    {
        var type = typeof(T);
        if (_componentPoolsInSpaces.TryGetValue(space, out var pools) && pools.TryGetValue(type, out var value))
        {
            var pool = (ComponentPool<T>)value;
            return pool.Components;
        }

        return [];
    }

    internal IEnumerable<T> GetComponentsByTypeInAvailable<T>(Space space)
    {
        var availableSpaces = SpaceTree.GetAllAvailableSpacesFor(space);
        for (int i = 0; i < availableSpaces.Count; i++)
        {
            foreach (var component in GetComponentsByType<T>(availableSpaces[i]))
            {
                yield return component;
            }
        }
    }

    internal T GetComponentByType<T>(Space space)
    {
        var type = typeof(T);
        if (_componentPoolsInSpaces.TryGetValue(space, out var pools) && pools.TryGetValue(type, out var value))
        {
            var pool = (ComponentPool<T>)value;
            if (pool.Components.Count > 0)
                return pool.Components[0];
        }

        return default;
    }

    internal T GetComponentByTypeInAvailable<T>(Space space)
    {
        var availableSpaces = SpaceTree.GetAllAvailableSpacesFor(space);
        for (int i = 0; i < availableSpaces.Count; i++)
        {
            var component = GetComponentByType<T>(availableSpaces[i]);
            if (component != null) return component;
        }

        return default;
    }

    internal IEnumerable<IComponentPool> GetComponentPoolsInSpace(Space space)
    {
        if (_componentPoolsInSpaces.TryGetValue(space, out var pools))
            return pools.Values;
        return Array.Empty<IComponentPool>();
    }

    #endregion
}