using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Composition;
using Microsoft.CodeAnalysis.CodeActions;
using System.Data;

namespace CacheCowLib.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CacheItVirtualAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CacheIt001";

    private static readonly LocalizableString Title = "Method must be virtual";
    private static readonly LocalizableString MessageFormat = "'{0}' should be declared virtual to be cached properly";
    private static readonly LocalizableString Description = "Non-virtual methods cannot be overridden, which is required for the caching mechanism to work.";
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
        var symbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration) as IMethodSymbol;

        if (symbol == null)
            return;

        var hasCacheItAttribute = symbol.GetAttributes().Any(attr => attr.AttributeClass.Name == "CacheItAttribute");

        if (hasCacheItAttribute && !symbol.IsVirtual)
        {
            var diagnostic = Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(), methodDeclaration.Identifier.Text);
            context.ReportDiagnostic(diagnostic);
        }
    }
}


