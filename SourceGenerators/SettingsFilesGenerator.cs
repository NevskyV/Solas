using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators;

[Generator]
public sealed class SettingsFileGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var structDeclaration = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, _) => ctx.Node as ClassDeclarationSyntax
            )
            .Where(static c => c is not null);

        var compilationAndStructs = context.CompilationProvider.Combine(structDeclaration.Collect());

        context.RegisterSourceOutput(compilationAndStructs, (spc, source) =>
        {
            var (compilation, structs) = source;
            var assemblyName = compilation.AssemblyName ?? "UnknownAssembly";
            foreach (var sds in structs)
            {
                if (sds == null) continue;
                var model = compilation.GetSemanticModel(sds.SyntaxTree);
                if (model.GetDeclaredSymbol(sds) is not INamedTypeSymbol symbol) continue;

                var attrs = symbol.GetAttributes();
                var attr = attrs.FirstOrDefault(a => a.AttributeClass?.Name == "SettingsSectionAttribute");
                if (attr == null) continue;

                var fullName = symbol.ToDisplayString();
                var className = symbol.Name;
                var ns = symbol.ContainingNamespace.ToDisplayString();

                var writer = new CodeWriter();
                writer.WriteLine("using System;");
                writer.WriteLine("using System.IO;");
                writer.WriteLine("using Solas.Attributes;");
                writer.WriteLine("using Solas.Components;");
                writer.WriteLine("using System.Runtime.CompilerServices;");
                writer.WriteLine();
                writer.WriteLine($"namespace {ns}");
                writer.WriteLine("{");
                writer.Indent();
                writer.WriteLine($"public partial class {className}");
                writer.WriteLine("{");
                writer.Indent();
                writer.WriteLine("[ModuleInitializer]");
                writer.WriteLine("public static void CreateBinary()");
                writer.WriteLine("{");
                writer.Indent();
                writer.WriteLine(
                    @"Engine.EnsureNeededDirectories(Directory.GetCurrentDirectory() + @""\Solas\"", Directory.GetCurrentDirectory() + @""\Solas\Settings\"", Directory.GetCurrentDirectory() + @""\Assets\"");");
                writer.WriteLine(@"var dir = Directory.GetCurrentDirectory() + @""\Solas\Settings\"";");
                writer.WriteLine($@"var path = dir + ""{className}.set"";");
                writer.WriteLine();
                writer.WriteLine("if (!File.Exists(path))");
                writer.WriteLine("{");
                writer.Indent();
                writer.WriteLine("using var stream = File.Open(path, FileMode.Create, FileAccess.Write);");
                writer.WriteLine("using var writer = new BinaryWriter(stream);");
                writer.WriteLine($@"writer.Write(""{fullName}, {assemblyName}"");");
                writer.WriteLine($"Write(writer, new {fullName}());");
                writer.Unindent();
                writer.WriteLine("}");
                writer.Unindent();
                writer.WriteLine("}");
                writer.Unindent();
                writer.WriteLine("}");
                writer.Unindent();
                writer.WriteLine("}");

                spc.AddSource($"{className}FileGenerator.g.cs", writer.ToString());
            }
        });
    }
}