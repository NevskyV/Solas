namespace Solas.Interfaces;

public interface IReferenceable
{
    public Guid Id { get; init; }

    public Guid GetSpaceId();

    internal static virtual IReferenceable SearchReferenceable<T>(Guid id, Guid spaceId) where T : class, IReferenceable
    {
        return null;
    }
}