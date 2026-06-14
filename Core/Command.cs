using Solas.Assets;
using Solas.Components;
using Solas.Interfaces;
using Solas.World;

namespace Solas;

public static class Command
{
    #region Settings System

    public static void WriteExistingSettings(IData settings)
    {
        EngineContext.SettingsSystem.WriteExistingSettings(settings);
    }

    public static void WriteNewSettings(IData settings, string path)
    {
        EngineContext.SettingsSystem.WriteNewSettings(settings, path);
    }

    #endregion

    #region DI System

    public static T AutoInject<T>(Space space) where T : Logic => 
        EngineContext.DISystem.AutoInject<T>(space);
    

    public static T Inject<T>(Guid id, Guid spaceId) where T : class, IReferenceable, new() =>
        EngineContext.DISystem.Inject<T>(id, spaceId);
    
    #endregion

    #region Assets Pool

    public static void RegisterNewAsset(Asset asset)
    {
        EngineContext.AssetsPool.RegisterNewAsset(asset);
    }

    public static void SaveAsset(Asset asset)
    {
        EngineContext.AssetsPool.SaveAsset(asset);
    }

    public static void SaveNewAssets()
    {
        EngineContext.AssetsPool.SaveNewAssets();
    }

    #endregion

    #region Space Pool

    public static Space LoadLocalSpace(string path, Space rootSpace = null)
    {
        return EngineContext.SpacePool.LoadLocalSpace(path, rootSpace);
    }

    public static void UnloadSpace(Space space)
    {
        EngineContext.SpacePool.UnloadSpace(space);
    }

    public static void SaveSpace(Space space)
    {
        EngineContext.SpacePool.SaveSpace(space);
    }

    #endregion

    #region Entity Pool

    public static void RegisterRunner(IUpdateRunner runner)
    {
        EngineContext.EntityPool.RegisterRunner(runner);
    }

    public static void RegisterFixedRunner(IUpdateRunner runner)
    {
        EngineContext.EntityPool.RegisterFixedRunner(runner);
    }

    public static void RegisterLateRunner(IUpdateRunner runner)
    {
        EngineContext.EntityPool.RegisterLateRunner(runner);
    }

    #endregion

    #region Registries

    public static void RegisterInjectMethods<T>(
        string typeName,
        Action<FileStream> write,
        Func<FileStream, (Guid, Guid)[]> read) =>
        EngineContext.InjectSerializationRegistry.Register<T>(typeName, write, read);

    public static void RegisterDataRead<T>(string typeName) where T : IData => EngineContext.DataReadingRegistry.Register<T>(typeName);
    public static void RegisterLogicAdd<T>(string typeName) where T : Logic, new() => EngineContext.LogicAddingRegistry.Register<T>(typeName);
    
    #endregion
}