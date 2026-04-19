using Core.Containers;
using Core.Systems;
using Core.World;

namespace Core;

public record struct AppContext(EntityPool EntityPool, Creator Creator, Destroyer Destroyer, Updater Updater);

public record struct WorldContext(Space GlobalSpace, Space[] LocalSpaces);