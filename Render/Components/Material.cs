using Solas.Assets;

namespace Solas.Render.Components;

public sealed class Material(MaterialDomain domain) : Asset
{
    public readonly MaterialDomain Domain = domain;
    private readonly List<ShaderModule> _modules = [];
    private readonly List<MaterialPass> _passes = [];
    private readonly List<List<ShaderModule>> _passModules = [];

    public event Action<Material, ShaderModule, Texture?>? OnTextureUpdated;

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
        module.OnTextureUpdated += HandleModuleTextureUpdated;
        RebuildPasses();
    }

    public void RemoveModule(ShaderModule module)
    {
        module.OnTextureUpdated -= HandleModuleTextureUpdated;
        _modules.Remove(module);
        RebuildPasses();
    }

    private void HandleModuleTextureUpdated(ShaderModule module, Texture? texture)
    {
        OnTextureUpdated?.Invoke(this, module, texture);
    }

    public IEnumerable<ModuleTextureBinding> GetAllTextureBindings()
    {
        foreach (var module in _modules)
        {
            foreach (var binding in module.GetTextureBindings())
            {
                yield return binding;
            }
        }
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

    public unsafe byte[] BuildPassUboData(int passIndex)
    {
        var modules = GetModulesForPass(passIndex);
        int totalSize = 0;

        foreach (var module in modules)
        {
            totalSize += module.SizeInBytes;
        }

        byte[] buffer = new byte[totalSize];

        fixed (byte* ptr = buffer)
        {
            byte* currentPtr = ptr;
            foreach (var module in modules)
            {
                module.WriteToBuffer(currentPtr);
                currentPtr += module.SizeInBytes;
            }
        }

        return buffer;
    }

    public unsafe byte[] BuildScreenUboData(int passAlignment = 256)
    {
        int totalSize = 0;
        int passCount = PassCount;

        for (int p = 0; p < passCount; p++)
        {
            int passSize = BuildPassUboData(p).Length;
            int paddedPassSize = (passSize + passAlignment - 1) & ~(passAlignment - 1);
            if (paddedPassSize == 0) paddedPassSize = passAlignment;
            totalSize += paddedPassSize;
        }

        byte[] buffer = new byte[totalSize];

        fixed (byte* ptr = buffer)
        {
            byte* currentPtr = ptr;
            for (int p = 0; p < passCount; p++)
            {
                var passBytes = BuildPassUboData(p);
                if (passBytes.Length > 0)
                {
                    fixed (byte* srcPtr = passBytes)
                    {
                        System.Buffer.MemoryCopy(srcPtr, currentPtr, passBytes.Length, passBytes.Length);
                    }
                }

                int paddedPassSize = (passBytes.Length + passAlignment - 1) & ~(passAlignment - 1);
                if (paddedPassSize == 0) paddedPassSize = passAlignment;
                currentPtr += paddedPassSize;
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