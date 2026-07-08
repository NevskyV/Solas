using System.Text;
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
            
            var sb = new StringBuilder();
            sb.AppendLine("""
                          using System;
                          using System.IO;
                          using Solas.Attributes;
                          using Solas.Components;
                          using Solas.Registries;
                          using System.Runtime.CompilerServices;
                          
                          namespace Solas.Generated
                          {
                              public class SettingsFileGenerator : ISettingsFilesRegistration
                              {
                                  private void CreateFile<T>(string fileName, string fullName) where T : new()
                                  {
                                      var dir = Solas.Query.GetPath("engine://Settings");
                                      var path = Path.Combine(dir, $"{fileName}.set");
                          
                                      if (!File.Exists(path))
                                      {
                                          using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
                                          Query.Serializer.Open(stream);
                                          Query.Serializer.Write(fullName, stream);
                                          try {
                                              Query.Serializer.Write(new T(), stream);
                                          }
                                          catch (Exception ex) {
                                              throw new Exception(ex.ToString());
                                          }
                                          Query.Serializer.Close(stream);
                                      }
                                  }
                                  public void Add(Registry registry)
                                  {
                                      var trueRegistry = (SettingsFilesRegistry) registry;
                          """);

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

                sb.AppendLine($"            trueRegistry.Register(() => CreateFile<{fullName}>(\"{className}\", \"{fullName + ", " + assemblyName}\"));");
            }

            sb.AppendLine("""
                                  }
                              }
                          }
                          """);

            spc.AddSource("SettingsFileGenerator.g.cs", sb.ToString());
        });
    }
}