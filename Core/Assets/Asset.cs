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

    public static IReferenceable SearchReferenceable<T>(Guid id, Guid spaceId) where T : IReferenceable
    {
        return EngineContext.AssetsPool.LoadAsset<T>(id);
    }
}