using Orbitality.Components;

namespace Orbitality.Interfaces;

public interface IComponentPool
{
    void Add(object component, Entity entity);
    void Remove(Entity entity);
}