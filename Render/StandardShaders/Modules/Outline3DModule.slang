module Outline3DModule;

import MaterialInterfaces;

public struct Outline3DParams
{
    public float4 outlineColor;
    public float thickness;
    private float3 pad;
};

[Domain3D]
[PassState("Front", true, true)]
public struct Outline3DModifier : IVertexModifier, IFragmentModifier
{
    public Outline3DParams params;

    public float3 ModifyVertex(float3 pos, float3 normal, float2 uv, float time)
    {
        if (cullMode == 1)
        {
            return pos + normal * params.thickness;
        }
        return pos;
    }

    public float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal, float3 camPos)
    {
        if (cullMode == 1)
        {
            return params.outlineColor;
        }
        return inColor;
    }
};