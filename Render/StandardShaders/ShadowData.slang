import LightData;

public struct ShadowMatrixData
{
    public float4x4 LightViewProj;
    public float4 LightPosAndRadius;
    public float4 ShadowParams;
};

public interface IShadowProvider
{
    float EvaluateShadow(Light light, float3 worldPos, float3 worldNormal, float viewDepth);
};

public float GetShadowBias(ShadowMatrixData shadowData)
{
    return shadowData.ShadowParams.x;
}

public float GetShadowStrength(ShadowMatrixData shadowData)
{
    return shadowData.ShadowParams.y;
}

public float GetShadowSoftness(ShadowMatrixData shadowData)
{
    return shadowData.ShadowParams.z;
}

public uint GetShadowLayerIndex(ShadowMatrixData shadowData)
{
    return (uint)max(shadowData.ShadowParams.w, 0.0f);
}
