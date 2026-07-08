using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Solas.SourceGenerators.Analysers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class OwnerProtectionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SOLAS0001";
    private const string Title = "Manual edit of 'Entity' property in IData is not allowed";
    private const string MessageFormat = "'Entity' property in IData can be modified only in Solas Engine Core";
    private const string Category = "Architecture";
    
    private const string CoreAssemblyName = "Solas.Core"; 

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId, Title, MessageFormat, Category, 
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeWithExpression, SyntaxKind.WithExpression);
    }

    private void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        if (context.Compilation.AssemblyName == CoreAssemblyName) return;

        var assignment = (AssignmentExpressionSyntax)context.Node;
        
        if (assignment.Left is MemberAccessExpressionSyntax { Name.Identifier.Text: "Entity" } memberAccess)
        {
            if (IsTargetingIData(memberAccess.Expression, context.SemanticModel))
            {
                context.ReportDiagnostic(Diagnostic.Create(_rule, assignment.GetLocation()));
            }
        }
    }

    private void AnalyzeWithExpression(SyntaxNodeAnalysisContext context)
    {
        if (context.Compilation.AssemblyName == CoreAssemblyName) return;

        var withExpression = (WithExpressionSyntax)context.Node;
        
        foreach (var initializer in withExpression.Initializer.Expressions)
        {
            if (initializer is AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.Text: "Entity" } })
            {
                if (IsTargetingIData(withExpression.Expression, context.SemanticModel))
                {
                    context.ReportDiagnostic(Diagnostic.Create(_rule, initializer.GetLocation()));
                }
            }
        }
    }

    private static bool IsTargetingIData(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        var typeInfo = semanticModel.GetTypeInfo(expression);
        var typeSymbol = typeInfo.Type;

        if (typeSymbol == null) return false;
        
        if (typeSymbol is { Name: "IData", TypeKind: TypeKind.Interface })
            return true;

        foreach (var @interface in typeSymbol.AllInterfaces)
        {
            if (@interface.Name == "IData")
                return true;
        }

        return false;
    }
}