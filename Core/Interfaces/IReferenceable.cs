namespace Solas.Interfaces;

public interface IReferenceable
{
    public Guid Id { get; init; }

    public Guid GetSpaceId();
    public void Write(BinaryWriter writer);
    public IReferenceable Read(BinaryReader reader);
}