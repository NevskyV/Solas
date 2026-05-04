using Orbitality.Components;

namespace Orbitality.Containers;

public struct ComponentPool<T>() : IComponentPool
{
    public List<T> Components { get; } = [];
    public List<Entity> Entities { get; } = [];

    private Dictionary<Entity, int> _indices = [];

    public void Add(object component, Entity entity)
    {
        var index = Components.Count;

        Components.Add((T)component);
        Entities.Add(entity);

        _indices[entity] = index;
    }

    public void Remove(Entity entity)
    {
        if (!_indices.TryGetValue(entity, out var index))
            return;

        var lastIndex = Components.Count - 1;

        Components[index] = Components[lastIndex];
        Entities[index] = Entities[lastIndex];

        _indices[Entities[index]] = index;

        Components.RemoveAt(lastIndex);
        Entities.RemoveAt(lastIndex);

        _indices.Remove(entity);
    }
}