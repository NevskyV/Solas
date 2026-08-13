module FrameParams;

public struct FrameParamsData
{
    public float4x4 LightViewProj;
    public float4 CameraPosition;
    public float4 CameraRight;
    public float4 CameraUp;
    public float4 CameraForward;
    public float4 ScreenResolution;
    public float4 TileCount;
    public uint TotalLightCount;
    public uint DirectionalLightCount;
    public float NearClip;
    public float FarClip;
    public uint IsOrthographic;
    public float TanHalfFovX;
    public float TanHalfFovY;
    public float _pad;
};