using Solas.Render.Components;
using Solas.Render.Logics;

namespace Solas.Render;

internal static class RenderLogicEventHandler
{
    private static List<MeshRenderLogic>? _loadedLogic = [];
    internal static Action<MeshRenderLogic> CreateLogicEvent = delegate { };
    internal static Action<MeshRenderLogic> DisposeLogicEvent = delegate { };
    internal static Action<MeshRenderLogic, Mesh> MeshUpdateEvent = delegate { };
    internal static Action<MeshRenderLogic, Texture> TextureUpdateEvent = delegate { };
    private static bool _haveBorrowed;

    internal static void RegisterData(MeshRenderLogic logic)
    {
        if (logic.Entity.IsNull) return;
        if (!_haveBorrowed) _loadedLogic.Add(logic);
        logic.MeshUpdate += mesh => MeshUpdateEvent.Invoke(logic, mesh);
        logic.TextureUpdate += texture => TextureUpdateEvent.Invoke(logic, texture);
        CreateLogicEvent.Invoke(logic);
    }

    internal static void UnregisterData(MeshRenderLogic logic)
    {
        if (logic.Entity.IsNull) return;
        logic.MeshUpdate -= mesh => MeshUpdateEvent.Invoke(logic, mesh);
        logic.TextureUpdate -= texture => TextureUpdateEvent.Invoke(logic, texture);
        DisposeLogicEvent.Invoke(logic);
    }

    internal static MeshRenderLogic[] BorrowLoadedLogic()
    {
        _haveBorrowed = true;
        var res = _loadedLogic.ToArray();
        _loadedLogic = null;
        return res;
    }
}