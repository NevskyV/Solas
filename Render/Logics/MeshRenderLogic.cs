using System.Numerics;
using Solas.Attributes;
using Solas.Components;
using Solas.Interfaces;
using Solas.Render.Components;
using Solas.Transform;
using Solas.Transform.MathExtensions;

namespace Solas.Render.Logics;

public partial class MeshRenderLogic : Logic, IInitializable
{
    [Inject]
    public Mesh? Mesh
    {
        get;
        set
        {
            field = value;
            MeshUpdate.Invoke(field);
        }
    }

    [Inject]
    public Texture? Texture
    {
        get;
        set
        {
            field = value;
            TextureUpdate.Invoke(field);
        }
    }

    private TransformData? _transformData;

    public Action<Texture> TextureUpdate = delegate { };
    public Action<Mesh> MeshUpdate = delegate { };

    public void Initialize()
    {
        _transformData = Entity.GetData<TransformData>() ?? Entity.AddData(new TransformData());
        RenderLogicEventHandler.RegisterData(this);
    }

    public Matrix4x4 GetModelMatrix()
    {
        var translationMat = Matrix4x4.CreateTranslation(_transformData!.Position.Value);
        var rotationMat = Matrix4x4.CreateFromQuaternion(_transformData.Rotation.Value.ToQuaternion());
        var scaleMat = Matrix4x4.CreateScale(_transformData.Scale.Value);

        return scaleMat * rotationMat * translationMat;
    }

    public override void Dispose()
    {
        RenderLogicEventHandler.UnregisterData(this);
    }
}