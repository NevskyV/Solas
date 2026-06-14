using Solas.Interfaces;

namespace Solas.Components;

public interface IData : IInjectable, IDisposable
{
    void IDisposable.Dispose()
    {
    }
}