import MaterialInterfaces;
import Lighting;
import LightData;
import Material3D;
import ShadowSampling;

[[vk::binding(2, 1)]] public Sampler2D normalMap;
[[vk::binding(3, 1)]] public Sampler2D roughnessTexture;
[[vk::binding(4, 1)]] public Sampler2D metallicTexture;
[[vk::binding(5, 1)]] public Sampler2D ambientTexture;

public struct Lit3DParams
{
    public float roughness;
    public float metallic;
    public float ambientIntensity;
    public float normalMapIntensity;
    public uint hasAlbedoTexture;
    public uint hasNormalMap;
    public uint hasRoughnessTexture;
    public uint hasMetallicTexture;
    public uint hasAmbientTexture;
    private float2 pad;
};

static const float PI = 3.14159265359f;
static const float INV_PI = 0.31830988618f;
static const float MIN_ROUGHNESS = 0.05f;
static const float EPSILON = 1e-6f;

[Domain3D]
public struct Lit3DModifier : IFragmentModifier
{
    public Lit3DParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal, float3 camPos)
    {
        const float3 N = normalize(worldNormal);
        const float3 V = normalize(camPos - worldPos);
        const float3 albedo = inColor.rgb;
        const float roughness = max(params.roughness, MIN_ROUGHNESS);
        const float metallic = params.metallic;
        const float3 F0 = lerp(float3(0.04f), albedo, metallic);
        const float NdotV = max(dot(N, V), EPSILON);
        const float3 ambient = albedo * params.ambientIntensity;
        float3 accumulatedLight = ambient;
        RasterShadowProvider shadowProvider;
        float4 shadowViewPosition = mul(ubo.view, float4(worldPos, 1.0f));
        float shadowViewDepth = -shadowViewPosition.z;

        for (uint i = 0u; i < ubo.directionalLightCount; ++i)
        {
            Light directionalLight = g_Lights[i];
            float3 L = normalize(-directionalLight.GetDirection());
            float NdotL = max(dot(N, L), 0.0f);

            if (NdotL > 0.0f)
            {
                float3 H = normalize(V + L);
                float3 lightColor = directionalLight.GetColor() * directionalLight.GetIntensity();
                float shadow = shadowProvider.EvaluateShadow(directionalLight, worldPos, N, shadowViewDepth);
                accumulatedLight += EvaluateBRDF(albedo, roughness, metallic, F0, N, V, H, L, NdotV, NdotL) *
                                    lightColor * NdotL * shadow;
            }
        }

        const uint3 tileCount = uint4(ubo.tileCount).xyz;
        const float nClip = max(ubo.nearClip, 0.001f);
        const float fClip = max(ubo.farClip, nClip + 0.001f);
        float4 viewPos4 = shadowViewPosition;
        float depth = shadowViewDepth;
        float4 clipPos = mul(ubo.proj, viewPos4);
        float inverseW = rcp(max(abs(clipPos.w), EPSILON));
        float2 screenUV = saturate(float2(
            clipPos.x * inverseW * 0.5f + 0.5f,
            -clipPos.y * inverseW * 0.5f + 0.5f));
        uint tileX = min(uint(screenUV.x * float(tileCount.x)), tileCount.x - 1u);
        uint tileY = min(uint(screenUV.y * float(tileCount.y)), tileCount.y - 1u);
        uint sliceZ = 0u;

        if (tileCount.z > 1u)
        {
            float depthClamped = clamp(depth, nClip, fClip);
            float factor = log2(depthClamped / nClip) * rcp(log2(fClip / nClip));
            sliceZ = min(uint(factor * float(tileCount.z)), tileCount.z - 1u);
        }

        uint clusterIndex = tileX + tileY * tileCount.x + sliceZ * (tileCount.x * tileCount.y);
        TileGrid tile = g_TileGrid[clusterIndex];

        for (uint lightOffset = 0u; lightOffset < tile.lightCount; ++lightOffset)
        {
            uint lightIndex = g_GlobalLightIndices[tile.lightOffset + lightOffset];
            Light localLight = g_Lights[lightIndex];
            float3 lightPosition = localLight.GetPosition();
            float radius = localLight.GetRadius();
            float3 lightVector = lightPosition - worldPos;
            float distanceSquared = dot(lightVector, lightVector);
            float radiusSquared = radius * radius;

            if (distanceSquared < radiusSquared && distanceSquared > EPSILON)
            {
                float distanceToLight = sqrt(distanceSquared);
                float3 L = lightVector * rcp(distanceToLight);
                float NdotL = max(dot(N, L), 0.0f);

                if (NdotL > 0.0f)
                {
                    float attenuation = saturate(1.0f - distanceSquared * rcp(radiusSquared));
                    float3 H = normalize(V + L);
                    float3 lightColor = localLight.GetColor() * localLight.GetIntensity() * attenuation;
                    float shadow = shadowProvider.EvaluateShadow(localLight, worldPos, N, shadowViewDepth);
                    accumulatedLight += EvaluateBRDF(albedo, roughness, metallic, F0, N, V, H, L, NdotV, NdotL) *
                                        lightColor * NdotL * shadow;
                }
            }
        }

        return float4(accumulatedLight, inColor.a);
    }

    float3 EvaluateBRDF(float3 albedo, float roughness, float metallic,
                        float3 F0, float3 N, float3 V, float3 H, float3 L,
                        float NdotV, float NdotL)
    {
        float3 F = FresnelSchlick(F0, V, H);
        float3 Kd = (1.0f - F) * (1.0f - metallic);
        float3 diffuse = Kd * albedo * INV_PI;
        float D = DistributionGGX(roughness, N, H);
        float G = GeometrySmith(roughness, NdotV, NdotL);
        float3 specular = (D * G * F) * rcp(4.0f * NdotV * NdotL + EPSILON);
        return diffuse + specular;
    }

    float DistributionGGX(float roughness, float3 N, float3 H)
    {
        float a = roughness;
        float a2 = a * a;
        float NdotH = max(dot(N, H), 0.0f);
        float NdotH2 = NdotH * NdotH;
        float denominator = NdotH2 * (a2 - 1.0f) + 1.0f;
        denominator = PI * denominator * denominator;
        return a2 * rcp(max(denominator, EPSILON));
    }

    float GeometrySchlickGGX(float NdotX, float roughness)
    {
        float k = roughness * roughness * 0.5f;
        return NdotX * rcp(NdotX * (1.0f - k) + k + EPSILON);
    }

    float GeometrySmith(float roughness, float NdotV, float NdotL)
    {
        return GeometrySchlickGGX(NdotV, roughness) * GeometrySchlickGGX(NdotL, roughness);
    }

    float3 FresnelSchlick(float3 F0, float3 V, float3 H)
    {
        float cosine = max(dot(V, H), 0.0f);
        float inverseCosine = 1.0f - cosine;
        float inverseCosineSquared = inverseCosine * inverseCosine;
        float inverseCosineFifth = inverseCosineSquared * inverseCosineSquared * inverseCosine;
        return F0 + (1.0f - F0) * inverseCosineFifth;
    }
};
