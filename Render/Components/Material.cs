using Solas.Assets;

namespace Solas.Render.Components;

public sealed class Material(MaterialDomain domain) : Asset
{
    public readonly MaterialDomain Domain = domain;
    private readonly List<ShaderModule> _modules = [];
    private readonly List<MaterialPass> _passes = [];

    public IReadOnlyList<ShaderModule> Modules => _modules;
    public IReadOnlyList<MaterialPass> Passes => _passes;

    public void AddModule(ShaderModule module)
    {
        if (module.Domain != ShaderDomain.Universal && (int)module.Domain != (int)Domain)
        {
            throw new InvalidOperationException(
                $"Cannot add module '{module.GetType().Name}' ({module.Domain}) to material with dimensions '{Domain}'."
            );
        }

        _modules.Add(module);
        RebuildPasses();
    }

    public void RemoveModule(ShaderModule module)
    {
        _modules.Remove(module);
        RebuildPasses();
    }

    private void RebuildPasses()
    {
        _passes.Clear();
        MaterialPass? currentPass = null;

        foreach (var module in _modules)
        {
            if (currentPass == null ||
                module.RequiresSeparatePass ||
                currentPass.Value.CullMode != module.RequiredCullMode)
            {
                currentPass = new MaterialPass
                {
                    CullMode = module.RequiredCullMode,
                    DepthWrite = module.RequiredDepthWrite
                };
                _passes.Add(currentPass.Value);
            }
        }
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
        var hash = Domain.ToString();

        foreach (var module in _modules)
        {
            hash += $"_{module.SlangModuleName}";
        }

        return hash;
    }
}