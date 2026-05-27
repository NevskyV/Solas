using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Orbitality.SourceGenerators;

[Generator]
public class UpdateRunnerGenerator : IIncrementalGenerator
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

            var runners = new StringBuilder();
            var register = new StringBuilder(@"
namespace Orbitality.Generated
{
    public static class GeneratedUpdateRegistration
    {
        public static void RegisterAll(Orbitality.Containers.EntityPool pool)
        {
");

            foreach (var cls in classes)
            {
                var model = compilation.GetSemanticModel(cls.SyntaxTree);
                if (model.GetDeclaredSymbol(cls) is not INamedTypeSymbol symbol) continue;

                var attrs = symbol.GetAttributes();

                Process(symbol, attrs, "UpdateAttribute", "Update", "RegisterRunner", runners, register);
                Process(symbol, attrs, "FixedUpdateAttribute", "FixedUpdate", "RegisterFixedRunner", runners, register);
                Process(symbol, attrs, "LateUpdateAttribute", "LateUpdate", "RegisterLateRunner", runners, register);
            }

            register.Append(@"
        }
    }
}");
            
            spc.AddSource(@"GeneratedUpdateRunners.g.cs", runners.ToString());
            spc.AddSource(@"GeneratedUpdateRegistration.g.cs", register.ToString());
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

        bool parallel = attr.NamedArguments
            .FirstOrDefault(a => a.Key == "Parallel").Value.Value as bool? ?? false;

        var runnerName = $"{className}_{methodName}Runner";
        
        var hasMethod = symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(m => m.Name == methodName && m.Parameters.Length == 0);

        if (!hasMethod)
            return;

        runners.Append($@"
using Orbitality.Containers;
namespace Orbitality.Generated
{{
    public class {runnerName} : Orbitality.Interfaces.IUpdateRunner
    {{
        public void Run()
        {{
            var comps = Engine.Context.EntityPool.GetComponentPoolsByType(typeof({fullName})).Cast<ComponentPool<{fullName}>>();");

        if (parallel)
        {
            runners.Append($@"
            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, comps.Count, 64),
                range =>
                {{
                    for (int i = range.Item1; i < range.Item2; i++)
                        comps[i].{methodName}();
                }});");
        }
        else
        {
            runners.Append($@"
            for (int i = 0; i < comps.Count; i++)
                comps[i].{methodName}();");
        }

        runners.Append(
            """
                    }
                }
            }
            """);

        register.Append($@"
            //pool.RegisterPool<{fullName}>();
            pool.{registerMethod}(new {runnerName}());
");
    }
}