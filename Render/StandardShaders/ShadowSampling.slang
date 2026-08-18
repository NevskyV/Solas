import LightData;
import Lighting;
import ShadowData;

float ComputePssmSplitForSampling(uint cascadeIndex, uint cascadeCount, float nearPlane, float farPlane, float splitLambda)
{
    float cascadeRatio = float(cascadeIndex) / float(max(cascadeCount, 1u));
    float logarithmic = nearPlane * pow(farPlane / nearPlane, cascadeRatio);
    float uniform = nearPlane + (farPlane - nearPlane) * cascadeRatio;
    return lerp(uniform, logarithmic, saturate(splitLambda));
}

uint SelectCascade(float viewDepth)
{
    uint cascadeCount = max(g_ShadowFrameParams.ShadowCascadeCount, 1u);
    float nearPlane = max(g_ShadowFrameParams.NearClip, 0.001f);
    float farPlane = max(min(g_ShadowFrameParams.FarClip, g_ShadowFrameParams.ShadowMaxDistance), nearPlane + 0.001f);

    for (uint cascadeIndex = 0u; cascadeIndex + 1u < cascadeCount; cascadeIndex++)
    {
        float splitFar = ComputePssmSplitForSampling(
            cascadeIndex + 1u,
            cascadeCount,
            nearPlane,
            farPlane,
            g_ShadowFrameParams.ShadowSplitLambda);
        if (viewDepth <= splitFar)
        {
            return cascadeIndex;
        }
    }

    return cascadeCount - 1u;
}

float ComputeSlopeScaledBias(ShadowMatrixData shadowData, float3 worldNormal, float3 toLight)
{
    float slope = 1.0f - saturate(dot(normalize(worldNormal), normalize(toLight)));
    return shadowData.ShadowParams.x * (1.0f + slope);
}

float SampleDepthArrayPcf(float3 shadowCoordinate, uint layerIndex, float comparisonDepth)
{
    float2 texelSize = rcp(float2(max(g_ShadowFrameParams.ShadowMapResolution, 1u), max(g_ShadowFrameParams.ShadowMapResolution, 1u)));
    float comparisonSum = 0.0f;
    float weightSum = 0.0f;

    for (int y = -2; y <= 2; y++)
    {
        float yWeight = 3.0f - abs(float(y));
        for (int x = -2; x <= 2; x++)
        {
            float xWeight = 3.0f - abs(float(x));
            float weight = xWeight * yWeight;
            float2 offset = float2(float(x), float(y)) * texelSize;
            comparisonSum += g_ShadowDepthArray.SampleCmpLevelZero(
                float3(shadowCoordinate.xy + offset, float(layerIndex)),
                comparisonDepth) * weight;
            weightSum += weight;
        }
    }

    return comparisonSum / max(weightSum, 0.0001f);
}

float SampleProjectedShadow(ShadowMatrixData shadowData, float3 worldPos, float3 worldNormal, float3 toLight)
{
    float4 projectedPosition = mul(shadowData.LightViewProj, float4(worldPos, 1.0f));
    float inverseW = rcp(max(abs(projectedPosition.w), 0.0001f));
    float3 shadowCoordinate = float3(
        projectedPosition.x * inverseW * 0.5f + 0.5f,
        projectedPosition.y * inverseW * 0.5f + 0.5f,
        projectedPosition.z * inverseW);

    if (shadowCoordinate.x <= 0.0f || shadowCoordinate.x >= 1.0f ||
        shadowCoordinate.y <= 0.0f || shadowCoordinate.y >= 1.0f ||
        shadowCoordinate.z <= 0.0f || shadowCoordinate.z >= 1.0f)
    {
        return 1.0f;
    }

    float bias = ComputeSlopeScaledBias(shadowData, worldNormal, toLight);
    return SampleDepthArrayPcf(shadowCoordinate, GetShadowLayerIndex(shadowData), shadowCoordinate.z - bias);
}

float SamplePointShadow(Light light, ShadowMatrixData shadowData, float3 worldPos, float3 worldNormal)
{
    float3 lightToSurface = worldPos - light.GetPosition();
    float distanceToLight = length(lightToSurface);
    float radius = max(light.GetRadius(), 0.001f);
    if (distanceToLight >= radius)
    {
        return 1.0f;
    }

    float3 toLight = -lightToSurface * rcp(max(distanceToLight, 0.0001f));
    float bias = ComputeSlopeScaledBias(shadowData, worldNormal, toLight);
    float normalizedLinearDepth = saturate(distanceToLight / radius - bias);
    uint cubeArrayIndex = (uint)max(light.extra1.z, 0.0f);
    return g_PointShadowCubeArray.SampleCmpLevelZero(
        float4(normalize(lightToSurface), float(cubeArrayIndex)),
        normalizedLinearDepth);
}

public struct RasterShadowProvider : IShadowProvider
{
    public float EvaluateShadow(Light light, float3 worldPos, float3 worldNormal, float viewDepth)
    {
        if (!light.CastsShadows())
        {
            return 1.0f;
        }

        uint shadowMatrixIndex = (uint)max(light.GetShadowMapIndex(), 0);
        uint lightType = light.GetLightType();
        float comparisonResult;
        ShadowMatrixData shadowData;

        if (lightType == 2u)
        {
            shadowMatrixIndex += SelectCascade(viewDepth);
            shadowData = g_ShadowMatrices[shadowMatrixIndex];
            comparisonResult = SampleProjectedShadow(shadowData, worldPos, worldNormal, -light.GetDirection());
        }
        else if (lightType == 1u)
        {
            shadowData = g_ShadowMatrices[shadowMatrixIndex];
            comparisonResult = SampleProjectedShadow(
                shadowData,
                worldPos,
                worldNormal,
                normalize(light.GetPosition() - worldPos));
        }
        else
        {
            shadowData = g_ShadowMatrices[shadowMatrixIndex];
            comparisonResult = SamplePointShadow(light, shadowData, worldPos, worldNormal);
        }

        return lerp(1.0f, comparisonResult, saturate(shadowData.ShadowParams.y));
    }
}
