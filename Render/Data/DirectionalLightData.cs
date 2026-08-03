using Solas.Render.Components;
using Solas.Transform;

namespace Solas.Render.Data;

public class DirectionalLightData : LightData
{
    internal override LightGpu GenerateGpuData() => 
        LightGpu.CreateDirectional
        (
            (Entity.GetData<TransformData>() ?? Entity.AddData(new TransformData())).Rotation.Value, 
            Color, 
            Intensity, 
            CastShadows,
            ShadowBias,
            ShadowSoftness,
            ShadowStrength
        );
}