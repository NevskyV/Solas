using Solas.Render.Components;
using Solas.Render.Data;

namespace Solas.Render;

internal static class LightDataEventHandler
{
    private static readonly Dictionary<LightData, LightGpu> _lights = [];
    internal static ReadOnlySpan<LightGpu> GpuLights => _lights.Values.ToArray();
    
    internal static void Register(LightData data)
    {
        if (data.Entity.IsNull) return;
        _lights.Add(data, data.GenerateGpuData());
    }

    internal static void OnLightUpdate(LightData data)
    {
        if (data.Entity.IsNull) return;
        _lights[data] = data.GenerateGpuData();
    }
    
    internal static void Unregister(LightData data)
    {
        if (data.Entity.IsNull) return;
        _lights.Remove(data);
    }
}