using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace CacheCowLib;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CachitRecursiveAnalyser : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CacheIt002";

    private static readonly LocalizableString Title = "Recursive functions need extra CacheCare";
    private static readonly LocalizableString MessageFormat = "'{0}' is recursive.";
    private static readonly LocalizableString Description = "Recursive functions need extra implementation care when caching.";
    private const string Category = "Usage";

    private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);


    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration) as IMethodSymbol;

        // Check if the method symbol is correctly obtained
        if (methodSymbol == null)
            return;

        // Search for invocations inside the method that refer to the method itself
        var invocations = methodDeclaration.DescendantNodes()
                                           .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invokedMethod = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

            // Check if the invoked method is the same as the containing method
            if (methodSymbol.Equals(invokedMethod))
            {
                // Recursive call found
                var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), methodSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}


