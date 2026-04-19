using Core.Containers;
using Core.World;

namespace Core;

public record struct AppContext(EntityPool EntityPool);

public record struct WorldContext(Space GlobalSpace, List<Space> LocalSpaces);