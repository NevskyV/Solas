using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Solas.SourceGenerators;

[Generator]
public class SlangShaderModuleGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var slangFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".slang", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => new SlangFileText
            {
                Path = file.Path,
                Content = file.GetText(cancellationToken)?.ToString() ?? string.Empty
            });

        context.RegisterSourceOutput(slangFiles.Collect(), static (spc, slangFiles) =>
        {
            var generatedClassNames = new List<string>();

            foreach (var slangFile in slangFiles)
            {
                var defaultModuleName = GetFileNameWithoutExtension(slangFile.Path);
                var parsedModules = ExtractSlangModules(slangFile.Content, defaultModuleName);

                foreach (var module in parsedModules)
                {
                    var generatedCode = GenerateCSharpModuleClass(module);
                    spc.AddSource($"{module.ClassName}.g.cs", SourceText.From(generatedCode, Encoding.UTF8));
                    generatedClassNames.Add(module.ClassName);
                }
            }

            var registryCode = GenerateRegistryClass(generatedClassNames);
            spc.AddSource("SlangModulesRegistry.g.cs", SourceText.From(registryCode, Encoding.UTF8));
        });
    }

    private static string GetFileNameWithoutExtension(string path)
    {
        if (string.IsNullOrEmpty(path)) return "SlangModule";
        var lastSlash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        var fileName = lastSlash >= 0 ? path.Substring(lastSlash + 1) : path;
        var dotIndex = fileName.LastIndexOf('.');
        return dotIndex > 0 ? fileName.Substring(0, dotIndex) : fileName;
    }

    private static List<SlangModuleInfo> ExtractSlangModules(string content, string defaultModuleName)
    {
        var result = new List<SlangModuleInfo>();

        var moduleMatch = Regex.Match(content, @"module\s+([A-Za-z0-9_]+);");
        var slangModuleName = moduleMatch.Success ? moduleMatch.Groups[1].Value : defaultModuleName;

        var modifierMatches = Regex.Matches(content, @"(?:\[\s*(Domain2D|Domain3D|DomainUniversal)\s*\][\s\r\n]*)?(?:public\s+|private\s+|internal\s+)?struct\s+([A-Za-z0-9_]+)(?:\s*:\s*(IFragmentModifier|IVertexModifier|[A-Za-z0-9_]+))?");

        foreach (Match match in modifierMatches)
        {
            var domainAttr = match.Groups[1].Value;
            var structName = match.Groups[2].Value;
            var modifierType = match.Groups[3].Value;

            if (structName.EndsWith("Params") || structName.EndsWith("Attribute") || structName.StartsWith("VS") || structName.StartsWith("Chain") || structName.Equals("UniformBuffer"))
            {
                continue;
            }

            var domain = domainAttr switch
            {
                "Domain2D" => "TwoD",
                "Domain3D" => "ThreeD",
                "DomainUniversal" => "Universal",
                _ => "Universal"
            };

            var fields = ExtractStructFields(content, structName);

            result.Add(new SlangModuleInfo
            {
                SlangModuleName = slangModuleName,
                ModifierStructName = structName,
                ClassName = structName.EndsWith("Modifier") ? structName.Substring(0, structName.Length - "Modifier".Length) + "Module" : (structName.EndsWith("Module") ? structName : structName + "Module"),
                GpuStructName = structName.EndsWith("Modifier") ? structName.Substring(0, structName.Length - "Modifier".Length) + "ParamsGpu" : structName + "GpuData",
                Domain = domain,
                ModifierType = modifierType,
                Fields = fields
            });
        }

        return result;
    }

    private static List<SlangFieldInfo> ExtractStructFields(string content, string structName)
    {
        var fields = new List<SlangFieldInfo>();

        var baseName = structName.EndsWith("Modifier") ? structName.Substring(0, structName.Length - "Modifier".Length) : structName;
        var paramsStructName = baseName + "Params";

        var paramsMatch = Regex.Match(content, @"(?:public\s+|private\s+|internal\s+)?struct\s+" + paramsStructName + @"\s*\{([^}]+)\}");
        if (paramsMatch.Success)
        {
            var paramsContent = paramsMatch.Groups[1].Value;
            fields.AddRange(ParseFieldLines(paramsContent));
        }

        if (fields.Count == 0)
        {
            var structMatch = Regex.Match(content, @"struct\s+" + structName + @"\s*:[^{]*\{([^}]+)\}");
            if (structMatch.Success)
            {
                var structContent = structMatch.Groups[1].Value;
                fields.AddRange(ParseFieldLines(structContent));
            }
        }

        return fields;
    }

    private static List<SlangFieldInfo> ParseFieldLines(string structContent)
    {
        var list = new List<SlangFieldInfo>();
        var lines = structContent.Split(';');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//") || trimmed.StartsWith("/*")) continue;

            var match = Regex.Match(trimmed, @"(?:public\s+|private\s+|internal\s+)?([A-Za-z0-9_]+)\s+([A-Za-z0-9_]+)");
            if (match.Success)
            {
                var type = match.Groups[1].Value;
                var name = match.Groups[2].Value;

                if (name.Equals("pad", StringComparison.OrdinalIgnoreCase) || type.Equals("ModifyColor") || type.Equals("ModifyVertex")) continue;

                list.Add(new SlangFieldInfo { SlangType = type, Name = name });
            }
        }
        return list;
    }

    private static string GenerateCSharpModuleClass(SlangModuleInfo module)
    {
        var structFieldsBuilder = new StringBuilder();
        var propertiesBuilder = new StringBuilder();

        var currentOffset = 0;

        foreach (var field in module.Fields)
        {
            var csType = MapSlangTypeToCSharp(field.SlangType);
            var csName = CapitalizeFirstLetter(field.Name);
            var (size, align) = GetTypeSizeAndAlignment(field.SlangType);

            var padding = (align - (currentOffset % align)) % align;
            if (padding > 0)
            {
                structFieldsBuilder.AppendLine($"    private fixed byte _pad{currentOffset}[{padding}];");
                currentOffset += padding;
            }

            structFieldsBuilder.AppendLine($"    public {csType} {csName};");
            currentOffset += size;

            propertiesBuilder.AppendLine($"    public {csType} {csName}");
            propertiesBuilder.AppendLine("    {");
            propertiesBuilder.AppendLine($"        get => Data.{csName};");
            propertiesBuilder.AppendLine($"        set => Data.{csName} = value;");
            propertiesBuilder.AppendLine("    }");
            propertiesBuilder.AppendLine();
        }

        var totalPadding = (16 - (currentOffset % 16)) % 16;
        if (totalPadding > 0)
        {
            structFieldsBuilder.AppendLine($"    private fixed byte _padEnd[{totalPadding}];");
            currentOffset += totalPadding;
        }

        var structSize = Math.Max(16, currentOffset);

        var paramsName = module.ModifierStructName.EndsWith("Modifier")
            ? module.ModifierStructName.Substring(0, module.ModifierStructName.Length - "Modifier".Length) + "Params"
            : module.ModifierStructName + "Params";

        return $@"namespace Solas.Render.Materials.Generated;

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Solas.Render;
using Solas.Render.Components;

[StructLayout(LayoutKind.Sequential, Size = {structSize})]
public unsafe struct {module.GpuStructName}
{{
{structFieldsBuilder.ToString().TrimEnd()}
}}

public unsafe class {module.ClassName} : ShaderModule
{{
    public {module.GpuStructName} Data;

    public override ShaderDomain Domain => ShaderDomain.{module.Domain};
    public override string SlangModuleName => ""{module.SlangModuleName}"";
    public override string SlangModifierName => ""{module.ModifierStructName}"";
    public override string SlangParamsName => ""{paramsName}"";
    public override bool IsVertexModifier => { (module.ModifierType == "IVertexModifier").ToString().ToLower() };
    public override bool IsFragmentModifier => { (module.ModifierType != "IVertexModifier").ToString().ToLower() };
    public override int SizeInBytes => sizeof({module.GpuStructName});

{propertiesBuilder.ToString().TrimEnd()}

    public override void WriteToBuffer(void* destinationPointer)
    {{
        *({module.GpuStructName}*)destinationPointer = Data;
    }}
}}
";
    }

    private static string GenerateRegistryClass(List<string> classNames)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Solas.Render.Materials.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class SlangModulesRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    public static readonly string[] GeneratedModules = new string[]");
        sb.AppendLine("    {");
        foreach (var name in classNames)
        {
            sb.AppendLine($"        \"{name}\",");
        }
        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string MapSlangTypeToCSharp(string slangType) => slangType switch
    {
        "float" => "float",
        "float2" => "Vector2",
        "float3" => "Vector3",
        "float4" => "Vector4",
        "float4x4" => "Matrix4x4",
        "int" => "int",
        "uint" => "uint",
        "bool" => "uint",
        _ => "float"
    };

    private static (int size, int align) GetTypeSizeAndAlignment(string slangType) => slangType switch
    {
        "float" => (4, 4),
        "float2" => (8, 8),
        "float3" => (12, 16),
        "float4" => (16, 16),
        "float4x4" => (64, 16),
        "int" => (4, 4),
        "uint" => (4, 4),
        "bool" => (4, 4),
        _ => (4, 4)
    };

    private static string CapitalizeFirstLetter(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        return char.ToUpperInvariant(str[0]) + str.Substring(1);
    }

    private class SlangFileText
    {
        public string Path { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class SlangModuleInfo
    {
        public string SlangModuleName { get; set; } = string.Empty;
        public string ModifierStructName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string GpuStructName { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string ModifierType { get; set; } = string.Empty;
        public List<SlangFieldInfo> Fields { get; set; } = new();
    }

    private class SlangFieldInfo
    {
        public string SlangType { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
