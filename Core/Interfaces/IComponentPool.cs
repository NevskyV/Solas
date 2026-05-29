using Solas.Components;

namespace Solas.Interfaces;

public interface IComponentPool
{
    void Add(object component, Entity entity);
    void Remove(Entity entity);
}