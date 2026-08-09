using Solas.Assets;

namespace Solas.Render.Components;

public sealed class Material(MaterialDomain domain) : Asset
{
    public readonly MaterialDomain Domain = domain;
    private readonly List<ShaderModule> _modules = [];
    private readonly List<MaterialPass> _passes = [];
    private readonly List<List<ShaderModule>> _passModules = [];

    public IReadOnlyList<ShaderModule> Modules => _modules;
    public IReadOnlyList<MaterialPass> Passes => _passes;
    public int PassCount => _passes.Count > 0 ? _passes.Count : 1;

    public IReadOnlyList<ShaderModule> GetModulesForPass(int passIndex)
    {
        if (passIndex >= 0 && passIndex < _passModules.Count)
        {
            return _passModules[passIndex];
        }

        return _modules;
    }

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
        _passModules.Clear();

        if (_modules.Count == 0) return;

        List<ShaderModule>? currentGroup = null;
        MaterialPass? currentPass = null;

        for (int i = 0; i < _modules.Count; i++)
        {
            var module = _modules[i];
            bool startNewPass = currentPass == null ||
                                module.RequiresSeparatePass ||
                                (i > 0 && _modules[i - 1].RequiresSeparatePass) ||
                                currentPass.Value.CullMode != module.RequiredCullMode;

            if (startNewPass)
            {
                currentPass = new MaterialPass
                {
                    CullMode = module.RequiredCullMode,
                    DepthWrite = module.RequiredDepthWrite
                };
                _passes.Add(currentPass.Value);
                currentGroup = [];
                _passModules.Add(currentGroup);
            }

            currentGroup!.Add(module);
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