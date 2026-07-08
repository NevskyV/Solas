using Solas.Interfaces;

namespace Solas.Components;

public interface IData : IInjectable, IDisposable
{
    public Entity Entity { get; set; }
    
    void IDisposable.Dispose()
    {
    }
}