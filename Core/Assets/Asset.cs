using Solas.Interfaces;

namespace Solas.Assets;

public abstract class Asset : IReferenceable
{
    protected Asset()
    {
        EngineContext.AssetsPool.RegisterNewAsset(this);
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid GetSpaceId() => Guid.Empty;
    public static IReferenceable SearchReferenceable<T>(Guid id, Guid spaceId) where T : class, IReferenceable, new() =>
        EngineContext.AssetsPool.LoadAsset<T>(id);
}