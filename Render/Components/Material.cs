
using Solas.Assets;

namespace Solas.Render.Components;

public sealed class Material : Asset
{
    public readonly Dimensions Dimensions;
    private readonly List<ShaderModule> _modules = [];

    public Material(Dimensions dimensions)
    {
        Dimensions = dimensions;
    }

    public IReadOnlyList<ShaderModule> Modules => _modules;

    public void AddModule(ShaderModule module)
    {
        if (module.Domain != ShaderDomain.Universal && (int)module.Domain != (int)Dimensions)
        {
            throw new InvalidOperationException(
                $"Cannot add module '{module.GetType().Name}' ({module.Domain}) to material with dimensions '{Dimensions}'."
            );
        }

        _modules.Add(module);
    }

    public void RemoveModule(ShaderModule module)
    {
        _modules.Remove(module);
    }

    public unsafe byte[] BuildCombinedUboData()
    {
        int totalSize = 0;
        
        foreach (var module in _modules)
        {
            totalSize += module.SizeInBytes;
        }

        byte[] buffer = new byte[totalSize];

        fixed (byte* ptr = buffer)
        {
            byte* currentPtr = ptr;
            foreach (var module in _modules)
            {
                module.WriteToBuffer(currentPtr);
                currentPtr += module.SizeInBytes;
            }
        }

        return buffer;
    }

    public string GetPipelineHash()
    {
        var hash = Dimensions.ToString();
        
        foreach (var module in _modules)
        {
            hash += $"_{module.SlangModuleName}";
        }
        
        return hash;
    }
}