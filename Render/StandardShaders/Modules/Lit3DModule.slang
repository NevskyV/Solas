module Lit3DModule;

import MaterialInterfaces;
import Lighting;
import LightData;
import Material3D;

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

[Domain3D]
public struct Lit3DModifier : IFragmentModifier
{
    public Lit3DParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal, float3 camPos)
    {
        float3 N = normalize(worldNormal);
        float3 V = normalize(camPos - worldPos);
        float3 baseAlbedo = inColor.rgb;

        float3 accumulatedLight = float3(0.0f);

        for (uint i = 0u; i < ubo.directionalLightCount; ++i)
        {
            Light dirLight = g_Lights[i];
            float3 L = normalize(-dirLight.GetDirection());
            float3 H = normalize(V + L);
            
            float3 lightColor = dirLight.GetColor() * dirLight.GetIntensity();
            accumulatedLight += PBR(baseAlbedo, params.roughness, N, V, H, L, lightColor, params);
        }
        return float4(accumulatedLight, inColor.a);
    }
    
    float3 PBR(float3 albedo, float alpha, float3 N, float3 V, float3 H, float3 L, float3 lightColor, Lit3DParams params)
    {
        float3 F0 = lerp(float3(0.04), albedo, params.metallic);
        float3 Ks = F(F0, V, H);
        float3 Kd = float3(1.0) - Ks * (1.0 - params.metallic);
        
        float3 lambert = albedo / PI;
        
        float LdotN = max(dot(L, N), 0.0);
        
        float3 cookTorranceNumerator = D(alpha, N, H) * G(alpha, N, V, L) * F(F0, V, H);
        float cookTorranceDenominator = 4.0 * max(dot(V, N), 0.001) * max(LdotN, 0.001);
        float3 cookTorrance = cookTorranceNumerator / cookTorranceDenominator;
        
        float3 BRDF = Kd * lambert + cookTorrance;
        float3 outgoingLight = albedo * params.ambientIntensity + BRDF * lightColor * LdotN; 
        return outgoingLight;
    }
    
    // GGX Normal Distribution Function
    float D(float roughness, float3 N, float3 H)
    {
        float alpha = max(roughness, 0.05f);
        float alpha2 = alpha * alpha;
        float NdotH = max(dot(N, H), 0.0f);
        float denom = (NdotH * NdotH * (alpha2 - 1.0f) + 1.0f);
        denom = PI * denom * denom;
        return alpha2 / max(denom, 0.000001f);
    }
    
    // Schlick-GGX Geometry Function
    float G1(float roughness, float3 N, float3 X)
    {
        float NdotX = max(dot(N, X), 0.0f);
        float k = (roughness * roughness) / 2.0f;
        float denom = NdotX * (1.0f - k) + k;
        return NdotX / max(denom, 0.000001f);
    }
    
    //Smith Model
    float G(float alpha, float3 N, float3 V, float3 L)
    {
        return G1(alpha, N, V) * G1(alpha, N, L);
    }
    
    //Fresnel-Schlick Function
    float3 F(float3 F0, float3 V, float3 H)
    {
        return F0 + (float3(1.0) - F0) * pow(1 - max(dot(V, H), 0.0), 5.0);
    }
};