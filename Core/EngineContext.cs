using Orbitality.Containers;
using Orbitality.Systems;

namespace Orbitality;

public record struct EngineContext(
    Creator Creator,
    Destroyer Destroyer,
    Updater Updater,
    EntityPool EntityPool,
    SpacePool SpacePool,
    DependencyInjector Injector,
    SettingsSystem SettingsSystem);