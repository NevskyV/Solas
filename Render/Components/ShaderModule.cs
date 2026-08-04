namespace Solas.Render.Components;

public abstract unsafe class ShaderModule
{
    public abstract ShaderDomain Domain { get; }
    public abstract string SlangModuleName { get; }
    public virtual string SlangModifierName => SlangModuleName.EndsWith("Module") ? SlangModuleName.Substring(0, SlangModuleName.Length - 6) + "Modifier" : SlangModuleName + "Modifier";
    public virtual string SlangParamsName => SlangModuleName.EndsWith("Module") ? SlangModuleName.Substring(0, SlangModuleName.Length - 6) + "Params" : SlangModuleName + "Params";
    public virtual bool IsVertexModifier => false;
    public virtual bool IsFragmentModifier => true;
    public abstract int SizeInBytes { get; }

    public abstract void WriteToBuffer(void* destinationPointer);
}