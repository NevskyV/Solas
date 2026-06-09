using Solas.Interfaces;

namespace Solas.Assets;

public abstract class Asset : IReferenceable
{
    protected Asset()
    {
        EngineContext.AssetsPool.RegisterNewAsset(this);
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid GetSpaceId()
    {
        return Guid.Empty;
    }

    public abstract void Write(BinaryWriter writer);
    public abstract IReferenceable Read(BinaryReader reader);
}