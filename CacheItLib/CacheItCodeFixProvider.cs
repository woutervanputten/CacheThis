using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace CacheCowLib
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CacheItCodeFixProvider)), Shared]
    public class CacheItCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Make method virtual";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(CacheItVirtualAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // Returns a fix all provider that can fix all the diagnostics in one go.
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the method declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => MakeVirtualAsync(context.Document, declaration, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private async Task<Document> MakeVirtualAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
        {
            // Create the virtual keyword token without additional trivia
            var virtualToken = SyntaxFactory.Token(SyntaxKind.VirtualKeyword);

            // Add the virtual modifier, retaining existing modifiers
            var newModifiers = methodDecl.Modifiers.Add(virtualToken);

            // Replace the old method declaration with the new one that includes the virtual keyword
            var newMethodDecl = methodDecl.WithModifiers(newModifiers);

            // Replace the old node with the new node in the syntax tree
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);

            // Optionally, format the modified syntax tree to ensure correct formatting
            var formattedRoot = Formatter.Format(newRoot, Formatter.Annotation, document.Project.Solution.Workspace);
            return document.WithSyntaxRoot(formattedRoot);
        }


        //private async Task<Document> MakeVirtualAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
        //{
        //    // Create a new virtual token with the necessary trivia (formatting)
        //    var virtualToken = SyntaxFactory.Token(SyntaxTriviaList.Create(SyntaxFactory.Space), SyntaxKind.VirtualKeyword, SyntaxTriviaList.Create(SyntaxFactory.Space));

        //    // Add the virtual modifier, retaining existing modifiers
        //    var newModifiers = methodDecl.Modifiers.Add(virtualToken);

        //    // Replace the old method declaration with the new one that includes the virtual keyword
        //    var newMethodDecl = methodDecl.WithModifiers(newModifiers);

        //    // Replace the old node with the new node in the syntax tree
        //    var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        //    var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);

        //    // Return a new document with the updated syntax tree
        //    return document.WithSyntaxRoot(newRoot);
        //}
    }
}
