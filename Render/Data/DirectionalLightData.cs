using Solas.Render.Components;
using Solas.Transform;
using Solas.Transform.MathExtensions;

namespace Solas.Render.Data;

public class DirectionalLightData : LightData
{
    internal override LightGpu GenerateGpuData() => 
        LightGpu.CreateDirectional
        (
            (Entity.GetData<TransformData>() ?? Entity.AddData(new TransformData())).Rotation.Value.ToQuaternion(), 
            Color, 
            Intensity, 
            CastShadows,
            ShadowBias,
            ShadowSoftness,
            ShadowStrength
        );
}