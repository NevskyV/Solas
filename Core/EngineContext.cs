using Solas.Containers;
using Solas.Systems;

namespace Solas;

public record struct EngineContext(
    DestroySystem Destroyer,
    UpdateSystem Updater,
    EntityPool EntityPool,
    SpacePool SpacePool,
    AssetsPool AssetsPool,
    DISystem DISystem,
    SettingsSystem SettingsSystem);