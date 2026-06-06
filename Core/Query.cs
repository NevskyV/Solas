using Solas.Assets;
using Solas.Components;
using Solas.World;

namespace Solas;

public static class Query
{
    #region Assets Pool

    public static T GetAsset<T>(Guid id) where T : Asset, new() => EngineContext.AssetsPool.GetAsset<T>(id);
    public static Asset GetLoadedAsset(Guid id) => EngineContext.AssetsPool.GetLoadedAsset(id);
    
    #endregion

    #region Space Pool

    

    #endregion
    
    #region Entity Pool

    public static Entity TryGetEntityFor(object component, Space hintSpace = null) => EngineContext.EntityPool.TryGetEntityFor(component, hintSpace);

    #endregion
}