using System.Numerics;
using Solas.Components;
using Solas.Render.Components;

namespace Solas.Render.Data;

public abstract class LightData : IData
{
    public Entity Entity
    {
        get;
        set
        {
            field = value;
            LightDataEventHandler.Register(this);
        }
    }

    public Dimensions Dimensions
    {
        get;
        set
        {
            field = value;
            LightDataEventHandler.OnLightUpdate(this);
        }
    } = Dimensions.ThreeD;

    public Vector3 Color
    {
        get;
        set
        {
            field = value;
            LightDataEventHandler.OnLightUpdate(this);
        }
    } = new Vector3(1, 1, 1);

    public float Intensity
    {
        get;
        set
        {
            field = value;
            LightDataEventHandler.OnLightUpdate(this);
        }
    } = 1f;

    public bool CastShadows
    {
        get;
        set
        {
            field = value;
            LightDataEventHandler.OnLightUpdate(this);
        }
    } = true;

    public float ShadowBias
    {
        get;
        set
        {
            field = value;
            LightDataEventHandler.OnLightUpdate(this);
        }
    } = 0.005f;

    public float ShadowSoftness
    {
        get;
        set
        {
            field = value;
            LightDataEventHandler.OnLightUpdate(this);
        }
    } = 1f;

    public float ShadowStrength
    {
        get;
        set
        {
            field = value;
            LightDataEventHandler.OnLightUpdate(this);
        }
    } = 1;

    internal abstract LightGpu GenerateGpuData();

    public void Dispose()
    {
        LightDataEventHandler.Unregister(this);
    }
}