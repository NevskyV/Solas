using Orbitality.Containers;
using Orbitality.Systems;

namespace Orbitality;

public record struct EngineContext(
    DestroySystem Destroyer,
    UpdateSystem Updater,
    EntityPool EntityPool,
    SpacePool SpacePool,
    DependencyInjectionSystem InjectionSystem,
    SettingsSystem SettingsSystem);