using System.Text;
using SlangShaderSharp;
using Solas.Render.Components;

namespace Solas.Render;

public sealed class SlangMaterialCompiler
{
    public static readonly SlangMaterialCompiler Instance = new(
    [
        Path.Combine(AppContext.BaseDirectory, "StandardShaders", "Embedded"),
        Path.Combine(AppContext.BaseDirectory, "StandardShaders"),
        Path.Combine(AppContext.BaseDirectory, "StandardShaders", "Modules"),
        ..Directory.GetDirectories(Query.GetPath("assets://"), "*", SearchOption.AllDirectories)
    ]);

    private readonly ISession _session;

    private SlangMaterialCompiler(string[] shaderDirectories)
    {
        Slang.CreateGlobalSession(0, out var globalSession);

        var sessionDesc = new SessionDesc
        {
            Targets = [new TargetDesc { Format = SlangCompileTarget.Spirv }],
            SearchPaths = shaderDirectories
        };

        globalSession.CreateSession(sessionDesc, out _session);
    }

    public unsafe byte[] CompileToSpirv(Material material, int passIndex = 0)
    {
        var components = new List<IComponentType>();

        var baseModules = material.Domain switch
        {
            MaterialDomain.TwoD => new[] { "MaterialInterfaces", "Material2D" },
            MaterialDomain.Screen => new[] { "MaterialInterfaces", "MaterialScreen" },
            MaterialDomain.ThreeD => new[]
            {
                "LightData", "ShadowData", "FrameParams", "Lighting", "MaterialInterfaces", "Material3D",
                "ShadowSampling"
            },
            _ => throw new ArgumentOutOfRangeException()
        };

        foreach (var baseModName in baseModules)
        {
            var baseMod = _session.LoadModule(baseModName, out var diag);
            CheckDiagnostics(diag);
            if (baseMod != null) components.Add(baseMod);
        }

        foreach (var mod in material.Modules)
        {
            var customMod = _session.LoadModule(mod.SlangModuleName, out var diag);
            CheckDiagnostics(diag);
            if (customMod != null) components.Add(customMod);
        }

        var passModules = material.GetModulesForPass(passIndex);

        List<ShaderModule> vertModules = [];
        List<ShaderModule> fragModules = [];

        foreach (var mod in passModules)
        {
            if (mod.IsVertexModifier) vertModules.Add(mod);
            if (mod.IsFragmentModifier) fragModules.Add(mod);
        }

        var vertChain = BuildVertexGenericChain(vertModules);
        var fragChain = BuildFragmentGenericChain(fragModules);
        var masterModule = material.Domain switch
        {
            MaterialDomain.TwoD => "Material2D",
            MaterialDomain.Screen => "MaterialScreen",
            MaterialDomain.ThreeD => "Material3D",
            _ => throw new ArgumentOutOfRangeException()
        };
        var bootstrapperCode =
            BuildBootstrapper(masterModule, material.Modules, passModules, vertModules, fragModules, vertChain,
                fragChain,
                material.Domain);

        var blob = Slang.CreateBlob(Encoding.UTF8.GetBytes(bootstrapperCode));

        var uniqueName = $"RuntimeLinker_{Guid.NewGuid():N}";
        var customLinkerMod =
            _session.LoadModuleFromSource(uniqueName, $"{uniqueName}.slang", blob, out var diag1);
        CheckDiagnostics(diag1);

        if (customLinkerMod == null)
            throw new Exception("Slang LoadModuleFromSource returned null.");

        customLinkerMod.FindEntryPointByName("fragmentMain", out var fragEntry);
        customLinkerMod.FindEntryPointByName("vertexMain", out var vertEntry);

        _session.CreateCompositeComponentType([.. components, customLinkerMod, vertEntry, fragEntry],
            out var linkedProgram, out var diag2);
        CheckDiagnostics(diag2);

        if (linkedProgram == null)
            throw new Exception("Slang CreateCompositeComponentType returned null.");

        linkedProgram.GetTargetCode(0, out var spirvBlob, out var diag3);
        CheckDiagnostics(diag3);

        if (spirvBlob == null)
            throw new Exception("Slang GetTargetCode returned null.");

        var size = (int)spirvBlob.GetBufferSize();
        var result = new ReadOnlySpan<byte>(spirvBlob.GetBufferPointer(), size).ToArray();

        return result;
    }

    public unsafe byte[] CompileEntryPointToSpirv(string moduleName, string entryPointName)
    {
        var components = new List<IComponentType>();
        if (moduleName.StartsWith("Shadow", StringComparison.Ordinal))
        {
            string[] dependencyModuleNames = ["LightData", "ShadowData", "FrameParams"];
            foreach (var dependencyModuleName in dependencyModuleNames)
            {
                if (dependencyModuleName == moduleName)
                {
                    continue;
                }

                var dependencyModule = _session.LoadModule(dependencyModuleName, out var dependencyDiagnostics);
                CheckDiagnostics(dependencyDiagnostics);
                if (dependencyModule == null)
                {
                    throw new InvalidOperationException(
                        $"Unable to load Slang dependency module '{dependencyModuleName}'.");
                }

                components.Add(dependencyModule);
            }
        }

        var module = _session.LoadModule(moduleName, out var moduleDiagnostics);
        CheckDiagnostics(moduleDiagnostics);
        if (module == null)
        {
            throw new InvalidOperationException($"Unable to load Slang module '{moduleName}'.");
        }

        module.FindEntryPointByName(entryPointName, out var entryPoint);
        if (entryPoint == null)
        {
            throw new InvalidOperationException(
                $"Unable to find Slang entry point '{entryPointName}' in module '{moduleName}'.");
        }

        components.Add(module);
        components.Add(entryPoint);
        _session.CreateCompositeComponentType([.. components], out var linkedProgram, out var linkDiagnostics);
        CheckDiagnostics(linkDiagnostics);
        if (linkedProgram == null)
        {
            throw new InvalidOperationException($"Unable to link Slang module '{moduleName}'.");
        }

        linkedProgram.GetTargetCode(0, out var spirvBlob, out var targetDiagnostics);
        CheckDiagnostics(targetDiagnostics);
        if (spirvBlob == null)
        {
            throw new InvalidOperationException($"Unable to produce SPIR-V for Slang module '{moduleName}'.");
        }

        var size = checked((int)spirvBlob.GetBufferSize());
        return new ReadOnlySpan<byte>(spirvBlob.GetBufferPointer(), size).ToArray();
    }

    private unsafe void CheckDiagnostics(ISlangBlob? diagnosticsBlob)
    {
        if (diagnosticsBlob != null)
        {
            var size = (int)diagnosticsBlob.GetBufferSize();
            if (size > 0)
            {
                var span = new ReadOnlySpan<byte>(diagnosticsBlob.GetBufferPointer(), size);
                var message = Encoding.UTF8.GetString(span);

                if (message.Contains("error"))
                {
                    throw new Exception($"Slang Compiler Diagnostic:\n{message}");
                }
            }
        }
    }

    private string BuildVertexGenericChain(IReadOnlyList<ShaderModule> modules)
    {
        if (modules.Count == 0)
        {
            return "ChainEndVertex";
        }

        var result = "ChainEndVertex";
        for (var i = modules.Count - 1; i >= 0; i--)
        {
            result = $"ChainNodeVertex<{modules[i].SlangModifierName}, {result}>";
        }

        return result;
    }

    private string BuildFragmentGenericChain(IReadOnlyList<ShaderModule> modules)
    {
        if (modules.Count == 0)
        {
            return "ChainEnd";
        }

        var result = "ChainEnd";
        for (var i = modules.Count - 1; i >= 0; i--)
        {
            result = $"ChainNode<{modules[i].SlangModifierName}, {result}>";
        }

        return result;
    }

    private string BuildBootstrapper(string master, IReadOnlyList<ShaderModule> allModules,
        IReadOnlyList<ShaderModule> passModules,
        List<ShaderModule> vertModules, List<ShaderModule> fragModules, string vertChain, string fragChain,
        MaterialDomain domain)
    {
        var sb = new StringBuilder();

        sb.AppendLine("import MaterialInterfaces;");
        sb.AppendLine($"import {master};");

        foreach (var mod in allModules)
        {
            sb.AppendLine($"import {mod.SlangModuleName};");
        }

        var moduleIndexMap = new Dictionary<ShaderModule, int>();
        for (var i = 0; i < allModules.Count; i++)
        {
            moduleIndexMap[allModules[i]] = i;
        }

        sb.AppendLine("struct MaterialParamsUBO");
        sb.AppendLine("{");
        var activeModules = domain == MaterialDomain.Screen ? passModules : allModules;

        for (var i = 0; i < activeModules.Count; i++)
        {
            if (activeModules[i].SizeInBytes > 0)
            {
                sb.AppendLine($"    {activeModules[i].SlangParamsName} modParams_{i};");
            }
        }

        sb.AppendLine("};");
        sb.AppendLine();
        var materialParamsBinding = domain == MaterialDomain.Screen ? 0 : 9;
        sb.AppendLine($"[[vk::binding({materialParamsBinding}, 1)]] ConstantBuffer<MaterialParamsUBO> matUbo;");
        sb.AppendLine();

        sb.AppendLine("[shader(\"vertex\")]");
        if (domain == MaterialDomain.Screen)
        {
            sb.AppendLine("public VSOutput vertexMain(uint vertexID : SV_VertexID)");
            sb.AppendLine("{");
            sb.AppendLine("    return screenVertMain(vertexID);");
            sb.AppendLine("}");
        }
        else
        {
            sb.AppendLine("public VSOutput vertexMain(VSInput input)");
            sb.AppendLine("{");
            if (vertModules.Count == 0)
            {
                sb.AppendLine("    ChainEndVertex vertChainInstance = {};");
            }
            else
            {
                sb.AppendLine($"    {vertChain} vertChainInstance;");
                for (var k = 0; k < vertModules.Count; k++)
                {
                    var mod = vertModules[k];
                    if (mod.SizeInBytes > 0)
                    {
                        var targetIdx = moduleIndexMap[mod];
                        var access = GetChainAccessPath(k);
                        sb.AppendLine($"    vertChainInstance.{access}.params = matUbo.modParams_{targetIdx};");
                    }
                }
            }

            sb.AppendLine($"    return vertMain<{vertChain}>(input, vertChainInstance);");
            sb.AppendLine("}");
        }

        sb.AppendLine();

        sb.AppendLine("[shader(\"fragment\")]");
        sb.AppendLine("public float4 fragmentMain(VSOutput input) : SV_Target");
        sb.AppendLine("{");
        if (fragModules.Count == 0)
        {
            sb.AppendLine("    ChainEnd fragChainInstance = {};");
        }
        else
        {
            sb.AppendLine($"    {fragChain} fragChainInstance;");
            for (var k = 0; k < fragModules.Count; k++)
            {
                var mod = fragModules[k];
                if (mod.SizeInBytes > 0)
                {
                    var targetIdx = domain == MaterialDomain.Screen ? k : moduleIndexMap[mod];
                    var access = GetChainAccessPath(k);
                    sb.AppendLine($"    fragChainInstance.{access}.params = matUbo.modParams_{targetIdx};");
                }
            }
        }

        if (domain == MaterialDomain.Screen)
        {
            sb.AppendLine($"    return screenFragMain<{fragChain}>(input, fragChainInstance);");
        }
        else
        {
            sb.AppendLine($"    return fragMain<{fragChain}>(input, fragChainInstance);");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GetChainAccessPath(int depth)
    {
        if (depth == 0) return "head";
        var sb = new StringBuilder();
        for (var i = 0; i < depth; i++)
        {
            sb.Append("tail.");
        }

        sb.Append("head");
        return sb.ToString();
    }
}