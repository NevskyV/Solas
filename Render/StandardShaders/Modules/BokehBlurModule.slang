module BokehBlurModule;

import MaterialInterfaces;
import MaterialScreen;

public struct BokehBlurParams
{
    public float blurRadius;
    public float bokehShape;
    public float chromaticAberration;
    public float brightnessThreshold;
};

[DomainScreen]
[PassState("None", false, true)]
public struct BokehBlurModifier : IFragmentModifier
{
    public BokehBlurParams params;

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float radius = params.blurRadius * 0.005f;
        if (radius <= 0.00001f)
        {
            return texture.Sample(uv);
        }

        float aberration = params.chromaticAberration * 0.002f;
        float thresh = params.brightnessThreshold;

        float4 accumulator = float4(0.0f, 0.0f, 0.0f, 0.0f);
        float totalWeight = 0.0f;

        const int SAMPLES = 32;
        const float GOLDEN_ANGLE = 2.39996323f;

        for (int i = 0; i < SAMPLES; i++)
        {
            float r = sqrt((float)i / (float)SAMPLES) * radius;
            float theta = i * GOLDEN_ANGLE;

            float2 offset = float2(cos(theta), sin(theta)) * r;

            if (params.bokehShape > 2.5f)
            {
                float blades = params.bokehShape;
                float angle = atan2(offset.y, offset.x);
                float slice = 6.2831853f / blades;
                float dist = cos(floor(0.5f + angle / slice) * slice - angle);
                offset *= dist;
            }

            float2 sampleUv = uv + offset;

            float4 col;
            if (aberration > 0.0001f)
            {
                col.r = texture.Sample(sampleUv + offset * aberration).r;
                col.g = texture.Sample(sampleUv).g;
                col.b = texture.Sample(sampleUv - offset * aberration).b;
                col.a = texture.Sample(sampleUv).a;
            }
            else
            {
                col = texture.Sample(sampleUv);
            }

            float luma = dot(col.rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float weight = 1.0f + max(0.0f, luma - thresh) * 5.0f;

            accumulator += col * weight;
            totalWeight += weight;
        }

        return accumulator / max(totalWeight, 0.0001f);
    }
};
