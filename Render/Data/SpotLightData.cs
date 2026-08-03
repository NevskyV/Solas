using Solas.Render.Components;
using Solas.Transform;

namespace Solas.Render.Data;

public class SpotLightData : LightData
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
    
    public float InnerAngleDegrees
    {
        get;
        set
        {
            field = value;
            LightDataEventHandler.OnLightUpdate(this);
        }
    }
    
    public float OuterAngleDegrees
    {
        get;
        set
        {
            field = value;
            LightDataEventHandler.OnLightUpdate(this);
        }
    }

    internal override LightGpu GenerateGpuData()
    {
        var transformData = (Entity.GetData<TransformData>() ?? Entity.AddData(new TransformData()));
        return LightGpu.CreateSpot
        (
            transformData.Position.Value, 
            transformData.Rotation.Value,
            Radius,
            InnerAngleDegrees,
            OuterAngleDegrees,
            Color,
            Intensity, 
            CastShadows,
            ShadowBias,
            ShadowSoftness,
            ShadowStrength
        );
    }
        
}