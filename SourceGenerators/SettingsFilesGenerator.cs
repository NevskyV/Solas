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

            var writer = new CodeWriter();
            writer.WriteLine("using System;");
            writer.WriteLine("using System.IO;");
            writer.WriteLine("using Solas.Attributes;");
            writer.WriteLine("using Solas.Components;");
            writer.WriteLine("using Solas.Serialization.Json;");
            writer.WriteLine("using System.Runtime.CompilerServices;");
            writer.WriteLine();
            writer.WriteLine("namespace Solas.Generated");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("public partial class SettingsFileGenerator");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("private static void CreateFile<T>(string fileName, string fullName) where T : new()");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine(@"var dir = Directory.GetCurrentDirectory() + @""\Solas\Settings\"";");
            writer.WriteLine(@"var path = dir + $""{fileName}.set"";");
            writer.WriteLine();
            writer.WriteLine("if (!File.Exists(path))");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("using var stream = File.Open(path, FileMode.Create, FileAccess.Write);");
            writer.WriteLine("Query.Serializer.Open(stream);");
            writer.WriteLine("Query.Serializer.Write(fullName, stream);");
            writer.WriteLine("try {");
            writer.Indent();
            writer.WriteLine("Query.Serializer.Write(new T(), stream);");
            writer.Unindent();
            writer.WriteLine("}");
            writer.WriteLine("catch (Exception ex) {");
            writer.Indent();
            writer.WriteLine("throw new Exception(ex.ToString());");
            writer.Unindent();
            writer.WriteLine("}");
            writer.WriteLine("Query.Serializer.Close(stream);");
            writer.Unindent();
            writer.WriteLine("}");
            writer.Unindent();
            writer.WriteLine("}");
            writer.WriteLine("public static void CreateFiles()");
            writer.WriteLine("{");
            writer.Indent();

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

                writer.WriteLine($"CreateFile<{fullName}>(\"{className}\", \"{fullName + ", " + assemblyName}\");");
            }

            writer.Unindent();
            writer.WriteLine("}");
            writer.Unindent();
            writer.WriteLine("}");
            writer.Unindent();
            writer.WriteLine("}");

            spc.AddSource("SettingsFileGenerator.g.cs", writer.ToString());
        });
    }
}