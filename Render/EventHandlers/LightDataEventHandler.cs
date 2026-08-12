using Solas.Render.Components;
using Solas.Render.Data;

namespace Solas.Render;

internal static class LightDataEventHandler
{
    private static readonly List<LightData> _registeredLights = [];
    private static LightGpu[] _cachedGpuArray = Array.Empty<LightGpu>();
    private static uint _cachedDirectionalCount;
    private static bool _isDirty = true;

    internal static ReadOnlySpan<LightGpu> GpuLights => GetGpuLights(out _);

    internal static uint DirectionalLightCount
    {
        get
        {
            if (_isDirty) UpdateGpuArray();
            return _cachedDirectionalCount;
        }
    }

    internal static void Register(LightData data)
    {
        if (data.Entity.IsNull) return;
        if (!_registeredLights.Contains(data))
        {
            _registeredLights.Add(data);
            _isDirty = true;
        }
    }

    internal static void OnLightUpdate(LightData data)
    {
        _isDirty = true;
    }

    internal static void Unregister(LightData data)
    {
        if (_registeredLights.Remove(data))
        {
            _isDirty = true;
        }
    }

    internal static ReadOnlySpan<LightGpu> GetGpuLights(out uint directionalCount)
    {
        _isDirty = true;
        if (_isDirty)
        {
            UpdateGpuArray();
        }

        directionalCount = _cachedDirectionalCount;
        return _cachedGpuArray;
    }

    private static void UpdateGpuArray()
    {
        if (_registeredLights.Count == 0)
        {
            _cachedGpuArray = Array.Empty<LightGpu>();
            _cachedDirectionalCount = 0;
            _isDirty = false;
            return;
        }

        var directionalList = new List<LightGpu>();
        var localList = new List<LightGpu>();

        for (var i = 0; i < _registeredLights.Count; i++)
        {
            var lightGpu = _registeredLights[i].GenerateGpuData();
            if (lightGpu.PositionOrDirection.W == 2.0f)
            {
                directionalList.Add(lightGpu);
            }
            else
            {
                localList.Add(lightGpu);
            }
        }

        _cachedDirectionalCount = (uint)directionalList.Count;

        var array = new LightGpu[directionalList.Count + localList.Count];
        directionalList.CopyTo(array, 0);
        localList.CopyTo(array, directionalList.Count);

        _cachedGpuArray = array;
        _isDirty = false;
    }
}