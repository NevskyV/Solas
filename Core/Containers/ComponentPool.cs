using Solas.Components;
using Solas.Interfaces;

namespace Solas.Containers;

public class ComponentPool<T> : IComponentPool
{
    private int[] _sparse = [];

    public List<T> Components { get; } = [];
    public List<Entity> Entities { get; } = [];

    public void Add(object component, Entity entity)
    {
        EnsureSparseCapacity(entity.InternalId);

        var index = Components.Count;

        Components.Add((T)component);
        Entities.Add(entity);

        _sparse[(int)entity.InternalId] = index;
    }

    public void Remove(Entity entity)
    {
        var id = entity.InternalId;
        if (id >= _sparse.Length) return;

        var index = _sparse[(int)id];
        if (index < 0 || index >= Components.Count || Entities[index] != entity)
            return;

        var lastIndex = Components.Count - 1;

        Components[index] = Components[lastIndex];
        Entities[index] = Entities[lastIndex];

        _sparse[(int)Entities[index].InternalId] = index;
        _sparse[(int)id] = -1;

        Components.RemoveAt(lastIndex);
        Entities.RemoveAt(lastIndex);
    }

    public Entity FindEntityFor(object component)
    {
        if (component is not T typed)
            return null;

        var index = Components.IndexOf(typed);
        return index >= 0 ? Entities[index] : null;
    }

    private void EnsureSparseCapacity(uint internalId)
    {
        var targetIndex = (int)internalId;
        if (targetIndex < _sparse.Length) return;

        int newSize = Math.Max(targetIndex + 1, _sparse.Length * 2);
        if (newSize < 1024) newSize = 1024;

        int oldSize = _sparse.Length;
        Array.Resize(ref _sparse, newSize);

        Array.Fill(_sparse, -1, oldSize, newSize - oldSize);
    }
}