using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Solas.SourceGenerators.Binary;
using Solas.SourceGenerators.Utils;

namespace Solas.SourceGenerators;

[Generator]
public sealed class BinarySerializerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var types =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => node is TypeDeclarationSyntax,
                    static (ctx, _) => (TypeDeclarationSyntax)ctx.Node);

        IncrementalValueProvider<(Compilation, ImmutableArray<TypeDeclarationSyntax>)> compilationAndTypes =
            context.CompilationProvider.Combine(types.Collect());

        context.RegisterSourceOutput(
            compilationAndTypes,
            static (ctx, source) =>
            {
                var (compilation, syntaxes) = source;

                var dataInterface = compilation.GetTypeByMetadataName("Solas.Components.IData");
                if (dataInterface == null) return;

                var referenceableInterface = compilation.GetTypeByMetadataName("Solas.Interfaces.IReferenceable");
                var entityType = compilation.GetTypeByMetadataName("Solas.Components.Entity");
                var logicType = compilation.GetTypeByMetadataName("Solas.Components.Logic");

                HashSet<INamedTypeSymbol> dataTypes = new(SymbolEqualityComparer.Default);

                foreach (var syntax in syntaxes)
                {
                    var semantic = compilation.GetSemanticModel(syntax.SyntaxTree);
                    if (semantic.GetDeclaredSymbol(syntax) is not INamedTypeSymbol symbol)
                        continue;

                    // КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: Исключаем интерфейсы, абстрактные классы и делегаты.
                    // Генератор должен работать исключительно с конкретными структурами и классами,
                    // которые можно создать через конструктор.
                    if (symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct) || symbol.IsAbstract)
                        continue;

                    if (!symbol.ImplementsInterface(dataInterface))
                        continue;

                    dataTypes.Add(symbol);
                }

                var genContext = new GenerationContext(
                    compilation,
                    referenceableInterface,
                    entityType,
                    logicType,
                    dataInterface);

                foreach (var type in dataTypes)
                    ctx.AddSource(
                        $"{type.Name}.Binary.g.cs",
                        SourceText.From(
                            BinarySerializationBuilder.Generate(type, genContext, compilation.AssemblyName),
                            Encoding.UTF8));
            });
    }

    public sealed class GenerationContext(
        Compilation compilation,
        INamedTypeSymbol? referenceableInterface,
        INamedTypeSymbol? entityType,
        INamedTypeSymbol? logicType,
        INamedTypeSymbol? dataInterface)
    {
        public Compilation Compilation { get; } = compilation;
        public INamedTypeSymbol? ReferenceableInterface { get; } = referenceableInterface;
        public INamedTypeSymbol? EntityType { get; } = entityType;
        public INamedTypeSymbol? LogicType { get; } = logicType;
        public INamedTypeSymbol? DataInterface { get; } = dataInterface;
    }

    public sealed class InjectableField
    {
        public required string Name { get; init; }
        public required ITypeSymbol Type { get; init; }
        public required bool IsDataProperty { get; init; }
    }
}