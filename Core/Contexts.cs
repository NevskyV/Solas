using Orbitality.Containers;
using Orbitality.Systems;
using Orbitality.World;

namespace Orbitality;

public record struct EngineContext(
    Creator Creator,
    Destroyer Destroyer,
    EntityPool EntityPool,
    SpaceSystem SpaceSystem);