using Solas.Components;

namespace Solas.Interfaces;

public interface IComponentPool
{
    public void Add(object component, Entity entity);
    public void Remove(Entity entity);
    public Entity FindEntityFor(object component);
}