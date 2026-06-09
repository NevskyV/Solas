using Solas.Components;
using Solas.Interfaces;

namespace Solas.Containers;

public class ComponentPool<T> : IComponentPool
{
    private readonly Dictionary<Entity, int> _indices = [];
    public List<T> Components { get; } = [];
    public List<Entity> Entities { get; } = [];

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

    public Entity FindEntityFor(object component)
    {
        if (component is not T typed)
            return null;

        var index = Components.IndexOf(typed);
        return index >= 0 ? Entities[index] : null;
    }
}