using Core.Containers;
using Core.Systems;
using Core.World;

namespace Core;

public record struct EngineContext(Creator Creator, Destroyer Destroyer, EntityPool EntityPool, SpaceSystem SpaceSystem);
public record struct WorldContext(Space GlobalSpace, HashSet<Space> LocalSpaces);