using Solas.Components;

namespace Solas.Containers;

public record struct DependencyPool
{
    private readonly Logic[] _dependencies;
    private uint _pointer;

    public DependencyPool(uint logicsCount)
    {
        _dependencies = new Logic[logicsCount];
    }
    
    public void Add<T>(T logic) where T : Logic
    {
        _dependencies[_pointer] = logic;
        _pointer++;
    }
    
    public T Get<T>() where T : Logic
    {
        return (T)_dependencies.First(x => x.GetType().IsAssignableFrom(typeof(T)));
    }
    
    public Logic[] GetAll()
    {
        return _dependencies.Clone() as Logic[];
    }
}