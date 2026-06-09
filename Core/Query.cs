using Solas.Assets;
using Solas.Components;
using Solas.World;

namespace Solas;

public static class Query
{
    #region Settings System

    public static T GetSettings<T>() where T : class, IData
    {
        return EngineContext.SettingsSystem.GetSettings<T>();
    }

    #endregion

    #region Assets Pool

    public static T GetAsset<T>(Guid id) where T : Asset, new()
    {
        return EngineContext.AssetsPool.GetAsset<T>(id);
    }

    public static Asset GetLoadedAsset(Guid id)
    {
        return EngineContext.AssetsPool.GetLoadedAsset(id);
    }

    #endregion

    #region Space Pool

    public static SpaceFolder GetSpaceFolderWith(Guid guid, Space space)
    {
        return EngineContext.SpacePool.GetSpaceFolderWith(guid, space);
    }

    public static SpaceFolder GetSpaceFolderWith(Guid guid, Guid spaceId)
    {
        return EngineContext.SpacePool.GetSpaceFolderWith(guid, spaceId);
    }

    public static IEnumerable<SpaceFolder> GetSpaceFoldersWith(List<Guid> guids, Space space)
    {
        return EngineContext.SpacePool.GetSpaceFoldersWith(guids, space);
    }

    public static List<SpaceFolder> GetAllSpaceFoldersIn(Space space)
    {
        return EngineContext.SpacePool.GetAllSpaceFoldersIn(space);
    }

    public static Space GetSpace(Guid guid)
    {
        return EngineContext.SpacePool.GetSpace(guid);
    }

    #endregion

    #region Entity Pool

    public static IEnumerable<Entity> GetEntitiesIn(Space space)
    {
        return EngineContext.EntityPool.GetEntitiesIn(space);
    }

    public static IEnumerable<Entity> GetEntitiesInAvailable(Space space)
    {
        return EngineContext.EntityPool.GetEntitiesInAvailable(space);
    }

    public static IEnumerable<Entity> GetEntitiesByType<T>(Space space)
    {
        return EngineContext.EntityPool.GetEntitiesByType<T>(space);
    }

    public static IEnumerable<Entity> GetEntitiesByTypeInAvailable<T>(Space space)
    {
        return EngineContext.EntityPool.GetEntitiesByTypeInAvailable<T>(space);
    }

    public static IEnumerable<Entity> GetEntitiesWith(Space space, params Type[] types)
    {
        return EngineContext.EntityPool.GetEntitiesWith(space, types);
    }

    public static IEnumerable<Entity> GetEntitiesInAvailableWith(Space space, params Type[] types)
    {
        return EngineContext.EntityPool.GetEntitiesInAvailableWith(space, types);
    }

    public static IEnumerable<T> GetComponentsByType<T>(Space space)
    {
        return EngineContext.EntityPool.GetComponentsByType<T>(space);
    }

    public static IEnumerable<T> GetComponentsByTypeInAvailable<T>(Space space)
    {
        return EngineContext.EntityPool.GetComponentsByTypeInAvailable<T>(space);
    }

    public static T GetComponentByType<T>(Space space)
    {
        return EngineContext.EntityPool.GetComponentByType<T>(space);
    }

    public static T GetComponentByTypeInAvailable<T>(Space space)
    {
        return EngineContext.EntityPool.GetComponentByTypeInAvailable<T>(space);
    }

    public static Entity TryGetEntityFor(object component, Space hintSpace = null)
    {
        return EngineContext.EntityPool.TryGetEntityFor(component, hintSpace);
    }

    #endregion
}