using Solas.Render.Components;
using Solas.Transform;

namespace Solas.Render.Data;

public class PointLightData : LightData
{
    public float Radius
    {
        get;
        set
        {
            field = value;
            LightDataEventHandler.OnLightUpdate(this);
        }
    }
    
    public float InnerAngleDeg
    {
        get;
        set
        {
            field = value;
            LightDataEventHandler.OnLightUpdate(this);
        }
    }
    
    public float OuterAngleDeg
    {
        get;
        set
        {
            field = value;
            LightDataEventHandler.OnLightUpdate(this);
        }
    }
    
    internal override LightGpu GenerateGpuData() => 
        LightGpu.CreatePoint
        (
            (Entity.GetData<TransformData>() ?? Entity.AddData(new TransformData())).Position.Value, 
            Radius,
            Color, 
            Intensity, 
            CastShadows,
            ShadowBias,
            ShadowSoftness,
            ShadowStrength
        );
}