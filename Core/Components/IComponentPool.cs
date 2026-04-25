namespace Core.Components;

public interface IComponentPool
{
    void Add(object component, Entity entity);
    void Remove(Entity entity);
}