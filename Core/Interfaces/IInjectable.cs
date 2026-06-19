using Solas.Components;

namespace Solas.Interfaces;

public interface IInjectable
{
    public void WriteInject(FileStream stream, Entity entity)
    {
    }

    public (Guid, Guid)[] ReadInject(FileStream stream)
    {
        return null;
    }

    public void Inject((Guid, Guid)[] guids)
    {
    }
}