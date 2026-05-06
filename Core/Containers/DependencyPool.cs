using Orbitality.Components;

namespace Orbitality.Containers;

public class DependencyPool(uint logicsCount)
{
    private readonly Logic[] _dependencies = new Logic[logicsCount];
    private uint _pointer;
    
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