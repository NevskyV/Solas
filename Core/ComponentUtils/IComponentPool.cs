using Orbitality.Components;

namespace Orbitality.ComponentUtils;

public interface IComponentPool
{
    void Add(object component, Entity entity);
    void Remove(Entity entity);
}