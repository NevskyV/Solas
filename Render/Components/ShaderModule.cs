namespace Solas.Render.Components;

public struct ModuleTextureBinding
{
    public int BindingIndex;
    public int SetIndex;
    public string Name;
    public Texture? Texture;
}

public abstract unsafe class ShaderModule
{
    public abstract ShaderDomain Domain { get; }
    public abstract string SlangModuleName { get; }

    public virtual string SlangModifierName => SlangModuleName.EndsWith("Module")
        ? SlangModuleName.Substring(0, SlangModuleName.Length - 6) + "Modifier"
        : SlangModuleName + "Modifier";

    public virtual string SlangParamsName => SlangModuleName.EndsWith("Module")
        ? SlangModuleName.Substring(0, SlangModuleName.Length - 6) + "Params"
        : SlangModuleName + "Params";

    public virtual bool IsVertexModifier => false;
    public virtual bool IsFragmentModifier => true;
    public abstract int SizeInBytes { get; }
    public virtual CullMode RequiredCullMode => CullMode.Back;
    public virtual bool RequiredDepthWrite => true;
    public virtual bool RequiresSeparatePass => false;
    public virtual MaterialPassPhase RequiredPassPhase => MaterialPassPhase.ObjectLocal;

    public event Action<ShaderModule, Texture?>? OnTextureUpdated;

    protected void NotifyTextureUpdated(Texture? texture)
    {
        OnTextureUpdated?.Invoke(this, texture);
    }

    public virtual IEnumerable<ModuleTextureBinding> GetTextureBindings()
    {
        return [];
    }

    public abstract void WriteToBuffer(void* destinationPointer);
}