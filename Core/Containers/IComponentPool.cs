using Orbitality.Components;

namespace Orbitality.Containers;

public interface IComponentPool
{
    void Add(object component, Entity entity);
    void Remove(Entity entity);
}