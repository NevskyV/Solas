using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators;

[Generator]
public sealed class InjectGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var fields = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is FieldDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) => (FieldDeclarationSyntax)ctx.Node
            );

        var combined = context.CompilationProvider.Combine(fields.Collect());

        context.RegisterSourceOutput(combined, (spc, source) =>
        {
            var (compilation, fieldsList) = source;
            var grouped = new Dictionary<INamedTypeSymbol, List<IFieldSymbol>>(SymbolEqualityComparer.Default);

            foreach (var fieldDecl in fieldsList)
            {
                var model = compilation.GetSemanticModel(fieldDecl.SyntaxTree);
                foreach (var v in fieldDecl.Declaration.Variables)
                {
                    if (model.GetDeclaredSymbol(v) is not IFieldSymbol fieldSymbol) continue;

                    var hasAttr = fieldSymbol.GetAttributes()
                        .Any(a => a.AttributeClass?.Name is "AutoInjectAttribute" or "InjectAttribute");

                    if (!hasAttr) continue;

                    var symbol = fieldSymbol.ContainingType;
                    if (!grouped.TryGetValue(symbol, out var list))
                    {
                        list = [];
                        grouped[symbol] = list;
                    }

                    list.Add(fieldSymbol);
                }
            }

            foreach (var pair in grouped)
            {
                var typeSymbol = pair.Key;
                var fieldsSymbols = pair.Value;
                var ns = typeSymbol.ContainingNamespace.ToDisplayString();
                var className = typeSymbol.Name;
                string keyword = typeSymbol.TypeKind == TypeKind.Struct ? "struct" : "class";

                var writer = new CodeWriter();
                writer.WriteLine("using System;");
                writer.WriteLine("using Solas;");
                writer.WriteLine();
                writer.WriteLine($"namespace {ns}");
                writer.WriteLine("{");
                writer.Indent();
                writer.WriteLine($"public partial {keyword} {className}");
                writer.WriteLine("{");
                writer.Indent();
                writer.WriteLine("public void Inject((Guid, Guid)[] guids)");
                writer.WriteLine("{");
                writer.Indent();

                var count = 0;
                foreach (var field in fieldsSymbols)
                {
                    if (field.GetAttributes().Any(a => a.AttributeClass?.Name == "AutoInjectAttribute"))
                    {
                        writer.WriteLine(
                            $"{field.Name} ??= Command.AutoInject<{field.Type.ToDisplayString()}>(Entity.CurrentSpace);");
                    }
                    else if (field.Type.BaseType?.ToDisplayString() == "Solas.Components.Logic")
                    {
                        writer.WriteLine(
                            $"{field.Name} ??= Command.Inject(guids[{count}].Item1, guids[{count}].Item2).GetLogic<{field.Type.ToDisplayString()}>();");
                    }
                    else if (field.Type.Interfaces.Any(x => x.ToDisplayString() == "Solas.Components.IData"))
                    {
                        writer.WriteLine(
                            $"{field.Name} ??= Command.Inject(guids[{count}].Item1, guids[{count}].Item2).GetData<{field.Type.ToDisplayString()}>();");
                    }
                    else
                    {
                        writer.WriteLine(
                            $"{field.Name} ??= Command.Inject<{field.Type.ToDisplayString()}>(guids[{count}].Item1);");
                    }

                    count++;
                }

                writer.Unindent();
                writer.WriteLine("}");
                writer.Unindent();
                writer.WriteLine("}");
                writer.Unindent();
                writer.WriteLine("}");

                spc.AddSource($"{className}.Inject.g.cs", writer.ToString());
            }
        });
    }
}