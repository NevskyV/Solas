using Solas.Interfaces;

namespace Solas.Assets;

public abstract class Asset : IReferenceable
{
    public Guid Id { get; init; }

    public Guid GetSpaceId() => Guid.Empty;
    public abstract void Write(BinaryWriter writer);
    public abstract IReferenceable Read(BinaryReader reader);

    public Asset()
    {
        Engine.Context.AssetsPool.RegisterNewAsset(this);
    }
}