using Solas.Containers;
using Solas.Registries;
using Solas.Serialization.Core;
using Solas.Systems;

namespace Solas;

internal record struct EngineContext
{
    //Systems
    internal static readonly DestroySystem Destroyer = new();
    internal static readonly UpdateSystem Updater = new();
    internal static readonly SettingsSystem SettingsSystem = new();
    internal static readonly DISystem DISystem = new();

    //Pools
    internal static readonly EntityPool EntityPool = new();
    internal static readonly SpacePool SpacePool = new();
    internal static readonly AssetsPool AssetsPool = new();
    
    //Serialization
    public static Serializer Serializer;
    public static InjectSerializationRegistry InjectSerializationRegistry;
    public static DataReadingRegistry DataReadingRegistry;
    public static LogicAddingRegistry LogicAddingRegistry;
}