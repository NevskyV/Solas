using System.Numerics;

namespace Solas.Transform;

public static class TransformEventHandler
{
    public static Action<TransformData> CreateDataEvent  = delegate {};
    public static Action<TransformData> DisposeDataEvent = delegate {};
    
    public static Action<TransformData, Vector3> PositionUpdate = delegate {};
    public static Action<TransformData, Vector3> RotationUpdate = delegate {};
    public static Action<TransformData, Vector3> ScaleUpdate    = delegate {};

    public static void RegisterData(TransformData data)
    {
        data.Position.OnChange += value => PositionUpdate(data, value);
        data.Rotation.OnChange += value => RotationUpdate(data, value);
        data.Scale.OnChange    += value => ScaleUpdate(data, value);
        CreateDataEvent.Invoke(data);
    }
    
    public static void UnregisterData(TransformData data)
    {
        data.Position.OnChange -= value => PositionUpdate(data, value);
        data.Rotation.OnChange -= value => RotationUpdate(data, value);
        data.Scale.OnChange    -= value => ScaleUpdate(data, value);
        DisposeDataEvent.Invoke(data);
    }
}