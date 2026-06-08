using System.Collections.Immutable;
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
                predicate: static (node, _) => node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0,
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
            )
            .Where(static c => c is not null);

        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses, (spc, source) =>
        {
            var (compilation, classes) = source;

            var runnersWriter = new CodeWriter();
            var registerWriter = new CodeWriter();

            runnersWriter.WriteLine("using System;");
            runnersWriter.WriteLine("using System.Collections.Generic;");
            runnersWriter.WriteLine("using Solas.Containers;");
            runnersWriter.WriteLine("using Solas.Interfaces;");
            runnersWriter.WriteLine();

            registerWriter.WriteLine("using System;");
            registerWriter.WriteLine();
            registerWriter.WriteLine("namespace Solas.Generated");
            registerWriter.WriteLine("{");
            registerWriter.Indent();
            registerWriter.WriteLine("internal static class GeneratedUpdateRegistration");
            registerWriter.WriteLine("{");
            registerWriter.Indent();
            registerWriter.WriteLine("internal static void RegisterAll()");
            registerWriter.WriteLine("{");
            registerWriter.Indent();

            foreach (var cls in classes)
            {
                if (cls == null) continue;
                var model = compilation.GetSemanticModel(cls.SyntaxTree);
                if (model.GetDeclaredSymbol(cls) is not INamedTypeSymbol symbol) continue;

                var attrs = symbol.GetAttributes();

                Process(symbol, attrs, "UpdateAttribute", "Update", "RegisterRunner", runnersWriter, registerWriter);
                Process(symbol, attrs, "FixedUpdateAttribute", "FixedUpdate", "RegisterFixedRunner", runnersWriter,
                    registerWriter);
                Process(symbol, attrs, "LateUpdateAttribute", "LateUpdate", "RegisterLateRunner", runnersWriter,
                    registerWriter);
            }

            registerWriter.Unindent();
            registerWriter.WriteLine("}");
            registerWriter.Unindent();
            registerWriter.WriteLine("}");
            registerWriter.Unindent();
            registerWriter.WriteLine("}");

            spc.AddSource("GeneratedUpdateRunners.g.cs", runnersWriter.ToString());
            spc.AddSource("GeneratedUpdateRegistration.g.cs", registerWriter.ToString());
        });
    }

    private static void Process(
        INamedTypeSymbol symbol,
        ImmutableArray<AttributeData> attrs,
        string attrName,
        string methodName,
        string registerMethod,
        CodeWriter runners,
        CodeWriter register)
    {
        var attr = attrs.FirstOrDefault(a => a.AttributeClass?.Name == attrName);
        if (attr == null) return;

        var fullName = symbol.ToDisplayString();
        var className = symbol.Name;

        bool parallel = attr.NamedArguments
            .FirstOrDefault(a => a.Key == "Parallel").Value.Value as bool? ?? false;

        var runnerName = $"{className}_{methodName}Runner";

        var hasMethod = symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(m => m.Name == methodName && m.Parameters.Length == 0);

        if (!hasMethod) return;

        runners.WriteLine("namespace Solas.Generated");
        runners.WriteLine("{");
        runners.Indent();
        runners.WriteLine($"internal class {runnerName} : Solas.Interfaces.IUpdateRunner");
        runners.WriteLine("{");
        runners.Indent();
        runners.WriteLine($"private readonly List<{fullName}> _updatables = new();");
        runners.WriteLine();
        runners.WriteLine("public void InjectPools(ReadOnlySpan<IComponentPool> pools)");
        runners.WriteLine("{");
        runners.Indent();
        runners.WriteLine("_updatables.Clear();");
        runners.WriteLine("foreach (var pool in pools)");
        runners.WriteLine("{");
        runners.Indent();
        runners.WriteLine($"if (pool is ComponentPool<{fullName}> castedPool)");
        runners.WriteLine("{");
        runners.Indent();
        runners.WriteLine("_updatables.AddRange(castedPool.Components);");
        runners.Unindent();
        runners.WriteLine("}");
        runners.Unindent();
        runners.WriteLine("}");
        runners.Unindent();
        runners.WriteLine("}");
        runners.WriteLine();
        runners.WriteLine("public void Run()");
        runners.WriteLine("{");
        runners.Indent();

        if (parallel)
        {
            runners.WriteLine("System.Threading.Tasks.Parallel.ForEach(");
            runners.Indent();
            runners.WriteLine("System.Collections.Concurrent.Partitioner.Create(0, _updatables.Count, 64),");
            runners.WriteLine("range =>");
            runners.WriteLine("{");
            runners.Indent();
            runners.WriteLine("for (int i = range.Item1; i < range.Item2; i++)");
            runners.WriteLine($"    _updatables[i].{methodName}();");
            runners.Unindent();
            runners.WriteLine("});");
            runners.Unindent();
        }
        else
        {
            runners.WriteLine("for (int i = 0; i < _updatables.Count; i++)");
            runners.WriteLine($"    _updatables[i].{methodName}();");
        }

        runners.Unindent();
        runners.WriteLine("}");
        runners.Unindent();
        runners.WriteLine("}");
        runners.Unindent();
        runners.WriteLine("}");
        runners.WriteLine();

        register.WriteLine($"Solas.Command.{registerMethod}(new {runnerName}());");
    }
}