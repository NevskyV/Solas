using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators;

[Generator]
public sealed class UpdateRunnerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
            );

        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses, (spc, source) =>
        {
            var (compilation, classes) = source;
            
            var runnersSb = new StringBuilder();
            var registerSb = new StringBuilder();

            runnersSb.AppendLine("""
                                 using System;
                                 using System.Collections.Generic;
                                 using Solas.Containers;
                                 using Solas.Interfaces;

                                 namespace SolasGenerated;
                                 
                                 """);


            registerSb.AppendLine("""
                                  using System;
                                  using Solas.Registries;

                                  namespace SolasGenerated
                                  {
                                      public class GeneratedUpdateRegistration : IUpdateRunnersRegistration
                                      {
                                          public void Add(Solas.Registries.Registry registry) => ((Solas.Registries.UpdateRunnersRegistry)registry).AddRegistration(this);
                                      
                                          public void RegisterAssembly()
                                          {
                                  """);

            foreach (var cls in classes)
            {
                var model = compilation.GetSemanticModel(cls.SyntaxTree);
                if (model.GetDeclaredSymbol(cls) is not INamedTypeSymbol symbol) continue;

                var attrs = symbol.GetAttributes();

                Process(symbol, attrs, "UpdateAttribute", "Update", "RegisterRunner", runnersSb, registerSb);
                Process(symbol, attrs, "FixedUpdateAttribute", "FixedUpdate", "RegisterFixedRunner", runnersSb,
                    registerSb);
                Process(symbol, attrs, "LateUpdateAttribute", "LateUpdate", "RegisterLateRunner", runnersSb,
                    registerSb);
            }

            registerSb.AppendLine("""
                                          }
                                      }
                                  }
                                  """);

            spc.AddSource("GeneratedUpdateRunners.g.cs", runnersSb.ToString());
            spc.AddSource("GeneratedUpdateRegistration.g.cs", registerSb.ToString());
        });
    }

    private static void Process(
        INamedTypeSymbol symbol,
        ImmutableArray<AttributeData> attrs,
        string attrName,
        string methodName,
        string registerMethod,
        StringBuilder runners,
        StringBuilder register)
    {
        var attr = attrs.FirstOrDefault(a => a.AttributeClass?.Name == attrName);
        if (attr == null) return;

        var fullName = symbol.ToDisplayString();
        var className = symbol.Name;

        var parallel = attr.NamedArguments
            .FirstOrDefault(a => a.Key == "Parallel").Value.Value as bool? ?? false;

        var runnerName = $"{className}_{methodName}Runner";

        var hasMethod = symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(m => m.Name == methodName && m.Parameters.Length == 0);

        if (!hasMethod) return;

        runners.AppendLine($$"""
                             internal class {{runnerName}} : Solas.Interfaces.IUpdateRunner
                             {
                                 private readonly List<{{fullName}}> _updatables = new();

                                 public void InjectPools(ReadOnlySpan<IComponentPool> pools)
                                 {
                                     _updatables.Clear();
                                     foreach (var pool in pools)
                                     {
                                         if (pool is ComponentPool<{{fullName}}> castedPool)
                                         {
                                             _updatables.AddRange(castedPool.Components);
                                         }
                                     }
                                 }

                                 public void Run()
                                 {
                             """);

        if (parallel)
        {
            runners.AppendLine("        System.Threading.Tasks.Parallel.ForEach(");
            runners.AppendLine("        System.Collections.Concurrent.Partitioner.Create(0, _updatables.Count, 64),");
            runners.AppendLine("        range =>");
            runners.AppendLine("        {");
            runners.AppendLine("            for (int i = range.Item1; i < range.Item2; i++)");
            runners.AppendLine($"               _updatables[i].{methodName}();");
            runners.AppendLine("        });");
        }
        else
        {
            runners.AppendLine("        for (int i = 0; i < _updatables.Count; i++)");
            runners.AppendLine($"           _updatables[i].{methodName}();");
        }

        runners.AppendLine("""
                               }
                           }
                           """);

        register.AppendLine($"          Solas.Command.{registerMethod}(new {runnerName}());");
    }
}