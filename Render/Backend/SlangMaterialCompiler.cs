using System.Text;
using SlangShaderSharp;
using Solas.Render.Components;

namespace Solas.Render;

public sealed class SlangMaterialCompiler
{
    public static readonly SlangMaterialCompiler Instance = new(
    [
        Path.Combine(AppContext.BaseDirectory, "StandardShaders"),
        Path.Combine(AppContext.BaseDirectory, "StandardShaders", "Modules"),
        ..Directory.GetDirectories(Query.GetPath("assets://"), "*", SearchOption.AllDirectories)
    ]);

    private readonly IGlobalSession _globalSession;
    private readonly ISession _session;

    public SlangMaterialCompiler(string[] shaderDirectories)
    {
        Slang.CreateGlobalSession(0, out _globalSession);

        var sessionDesc = new SessionDesc
        {
            Targets = [new TargetDesc { Format = SlangCompileTarget.Spirv }],
            SearchPaths = shaderDirectories
        };

        _globalSession.CreateSession(sessionDesc, out _session);
    }

    public unsafe byte[] CompileToSpirv(Material material, int passIndex = 0)
    {
        var components = new List<IComponentType>();

        var baseModules = material.Domain switch
        {
            MaterialDomain.TwoD => new[] { "MaterialInterfaces", "Material2D" },
            MaterialDomain.Screen => new[] { "MaterialInterfaces", "MaterialScreen" },
            _ => new[] { "LightData", "Lighting", "MaterialInterfaces", "Material3D" }
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

        string vertChain = BuildVertexGenericChain(vertModules);
        string fragChain = BuildFragmentGenericChain(fragModules);
        string masterModule = material.Domain switch
        {
            MaterialDomain.TwoD => "Material2D",
            MaterialDomain.Screen => "MaterialScreen",
            _ => "Material3D"
        };
        string bootstrapperCode =
            BuildBootstrapper(masterModule, material.Modules, passModules, vertModules, fragModules, vertChain,
                fragChain,
                material.Domain);

        var blob = Slang.CreateBlob(Encoding.UTF8.GetBytes(bootstrapperCode));

        string uniqueName = $"RuntimeLinker_{Guid.NewGuid():N}";
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

        int size = (int)spirvBlob.GetBufferSize();
        byte[] result = new ReadOnlySpan<byte>(spirvBlob.GetBufferPointer(), size).ToArray();

        return result;
    }

    private unsafe void CheckDiagnostics(ISlangBlob? diagnosticsBlob)
    {
        if (diagnosticsBlob != null)
        {
            int size = (int)diagnosticsBlob.GetBufferSize();
            if (size > 0)
            {
                var span = new ReadOnlySpan<byte>(diagnosticsBlob.GetBufferPointer(), size);
                string message = Encoding.UTF8.GetString(span);

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

        string result = "ChainEndVertex";
        for (int i = modules.Count - 1; i >= 0; i--)
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

        string result = "ChainEnd";
        for (int i = modules.Count - 1; i >= 0; i--)
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
        for (int i = 0; i < allModules.Count; i++)
        {
            moduleIndexMap[allModules[i]] = i;
        }

        sb.AppendLine("struct MaterialParamsUBO");
        sb.AppendLine("{");
        if (domain != MaterialDomain.Screen)
        {
            sb.AppendLine("    UniformBuffer baseUbo;");
        }

        var activeModules = domain == MaterialDomain.Screen ? passModules : allModules;

        for (int i = 0; i < activeModules.Count; i++)
        {
            if (activeModules[i].SizeInBytes > 0)
            {
                sb.AppendLine($"    {activeModules[i].SlangParamsName} modParams_{i};");
            }
        }

        sb.AppendLine("};");
        sb.AppendLine();
        sb.AppendLine("[[vk::binding(0, 1)]] ConstantBuffer<MaterialParamsUBO> matUbo;");
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
                for (int k = 0; k < vertModules.Count; k++)
                {
                    var mod = vertModules[k];
                    if (mod.SizeInBytes > 0)
                    {
                        int targetIdx = domain == MaterialDomain.Screen ? k : moduleIndexMap[mod];
                        string access = GetChainAccessPath(k);
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
            for (int k = 0; k < fragModules.Count; k++)
            {
                var mod = fragModules[k];
                if (mod.SizeInBytes > 0)
                {
                    int targetIdx = domain == MaterialDomain.Screen ? k : moduleIndexMap[mod];
                    string access = GetChainAccessPath(k);
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
        for (int i = 0; i < depth; i++)
        {
            sb.Append("tail.");
        }

        sb.Append("head");
        return sb.ToString();
    }
}