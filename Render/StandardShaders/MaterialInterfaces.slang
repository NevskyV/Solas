module MaterialInterfaces;

[[vk::push_constant]]
public cbuffer PassConstant
{
    public uint cullMode;
    public uint depthWrite;
};

[__AttributeUsage(_AttributeTargets.Struct)]
public struct Domain2DAttribute {};
[__AttributeUsage(_AttributeTargets.Struct)]
public struct Domain3DAttribute {};
[__AttributeUsage(_AttributeTargets.Struct)]
public struct DomainUniversalAttribute {};
[__AttributeUsage(_AttributeTargets.Struct)]
public struct DomainScreenAttribute {};

[__AttributeUsage(_AttributeTargets.Struct)]
public struct PassStateAttribute
{
    public string cullMode;
    public bool depthWrite;
    public bool separatePass;
};

public interface IVertexModifier
{
    float3 ModifyVertex(float3 pos, float3 normal, float2 uv, float time);
};

public interface IFragmentModifier
{
    float4 ModifyColor(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal);
};

public interface IVertexChain
{
    float3 ProcessChain(float3 pos, float3 normal, float2 uv, float time);
};

public interface IFragmentChain
{
    float4 ProcessChain(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal);
};

public struct ChainEndVertex : IVertexChain
{
    public float3 ProcessChain(float3 pos, float3 normal, float2 uv, float time)
    {
        return pos;
    }
};

public struct ChainNodeVertex<THead : IVertexModifier, TTail : IVertexChain> : IVertexChain
{
    public THead head;
    public TTail tail;
    public float3 ProcessChain(float3 pos, float3 normal, float2 uv, float time)
    {
        float3 v = head.ModifyVertex(pos, normal, uv, time);
        return tail.ProcessChain(v, normal, uv, time);
    }
};

public struct ChainEnd : IFragmentChain
{
    public float4 ProcessChain(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        return inColor;
    }
};

public struct ChainEndFragment : IFragmentChain
{
    public float4 ProcessChain(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        return inColor;
    }
};

public struct ChainNode<THead : IFragmentModifier, TTail : IFragmentChain> : IFragmentChain
{
    public THead head;
    public TTail tail;
    public float4 ProcessChain(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float4 c = head.ModifyColor(inColor, uv, worldPos, worldNormal);
        return tail.ProcessChain(c, uv, worldPos, worldNormal);
    }
};

public struct ChainNodeFragment<THead : IFragmentModifier, TTail : IFragmentChain> : IFragmentChain
{
    public THead head;
    public TTail tail;
    public float4 ProcessChain(float4 inColor, float2 uv, float3 worldPos, float3 worldNormal)
    {
        float4 c = head.ModifyColor(inColor, uv, worldPos, worldNormal);
        return tail.ProcessChain(c, uv, worldPos, worldNormal);
    }
};
