using Solas.Render.Components;
using Solas.Render.Logics;

namespace Solas.Render;

internal static class RenderLogicEventHandler
{
    private static List<MeshRenderLogic> _loadedLogic = [];
    internal static Action<MeshRenderLogic> CreateLogicEvent = delegate { };
    internal static Action<MeshRenderLogic> DisposeLogicEvent = delegate { };
    internal static Action<MeshRenderLogic, Mesh> MeshUpdateEvent = delegate { };
    internal static Action<MeshRenderLogic, Texture> TextureUpdateEvent = delegate { };
    internal static Action<MeshRenderLogic, Material> MaterialUpdateEvent = delegate { };
    private static bool _haveBorrowed;

    internal static void Register(MeshRenderLogic logic)
    {
        if (logic.Entity.IsNull) return;
        if (!_haveBorrowed) _loadedLogic.Add(logic);
        CreateLogicEvent.Invoke(logic);
    }

    internal static void OnMeshUpdate(MeshRenderLogic logic, Mesh mesh)
    {
        MeshUpdateEvent.Invoke(logic, mesh);
    }
    
    internal static void OnTextureUpdate(MeshRenderLogic logic, Texture texture)
    {
        TextureUpdateEvent.Invoke(logic, texture);
    }
    
    internal static void OnMaterialUpdate(MeshRenderLogic logic, Material material)
    {
        MaterialUpdateEvent.Invoke(logic, material);
    }

    internal static void Unregister(MeshRenderLogic logic)
    {
        if (logic.Entity.IsNull) return;
        DisposeLogicEvent.Invoke(logic);
    }

    internal static MeshRenderLogic[] BorrowLoadedLogic()
    {
        _haveBorrowed = true;
        var res = _loadedLogic.ToArray();
        _loadedLogic = null!;
        return res;
    }
}