using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DraasGames.Unity.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnityCallbackOrderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DGUA002";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Unity callback order",
            "Unity callback '{0}' appears after preceding method '{1}', but the configured order requires '{0}' before it",
            "DraasGames.Unity",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Unity callback methods should precede ordinary methods and follow the configured callback order within each type declaration.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(startContext =>
            {
                startContext.CancellationToken.ThrowIfCancellationRequested();

                AnalyzerSettings settings = AnalyzerSettings.Load(
                    startContext.Options,
                    startContext.CancellationToken);

                startContext.RegisterSyntaxNodeAction(
                    syntaxContext => AnalyzeTypeDeclaration(syntaxContext, settings),
                    SyntaxKind.ClassDeclaration);
            });
        }

        private static void AnalyzeTypeDeclaration(
            SyntaxNodeAnalysisContext context,
            AnalyzerSettings settings)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            ClassDeclarationSyntax typeDeclaration = (ClassDeclarationSyntax)context.Node;
            INamedTypeSymbol? typeSymbol = context.SemanticModel.GetDeclaredSymbol(
                typeDeclaration,
                context.CancellationToken);

            if (typeSymbol is null)
            {
                return;
            }

            if (!UnityCallbackClassifier.IsUnityType(typeSymbol))
            {
                return;
            }

            AnalyzerSettings effectiveSettings = settings.ForTree(
                context.Options.AnalyzerConfigOptionsProvider,
                typeDeclaration.SyntaxTree);
            Dictionary<string, int> rankByCallback = CreateRankMap(effectiveSettings.CallbackOrder);

            int highestRank = -1;
            string? highestCallbackName = null;
            string? firstOrdinaryMethodName = null;

            foreach (MemberDeclarationSyntax member in typeDeclaration.Members)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (member is not MethodDeclarationSyntax methodDeclaration)
                {
                    continue;
                }

                string methodName = methodDeclaration.Identifier.ValueText;
                IMethodSymbol? methodSymbol = context.SemanticModel.GetDeclaredSymbol(
                    methodDeclaration,
                    context.CancellationToken);
                if (methodSymbol is null
                    || !UnityCallbackClassifier.IsUnityMethod(
                        methodSymbol,
                        context.CancellationToken))
                {
                    firstOrdinaryMethodName ??= methodName;
                    continue;
                }

                bool placementViolation = effectiveSettings.CallbacksBeforeOtherMethods
                    && firstOrdinaryMethodName is not null;
                bool hasConfiguredRank = rankByCallback.TryGetValue(
                    methodName,
                    out int callbackRank);
                bool relativeOrderViolation = hasConfiguredRank
                    && highestRank >= 0
                    && callbackRank < highestRank;
                if (placementViolation || relativeOrderViolation)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Rule,
                        methodDeclaration.Identifier.GetLocation(),
                        methodName,
                        placementViolation ? firstOrdinaryMethodName! : highestCallbackName!));
                }

                if (!hasConfiguredRank)
                {
                    continue;
                }

                if (callbackRank > highestRank)
                {
                    highestRank = callbackRank;
                    highestCallbackName = methodName;
                }
            }
        }

        private static Dictionary<string, int> CreateRankMap(ImmutableArray<string> callbackOrder)
        {
            var rankByCallback = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < callbackOrder.Length; index++)
            {
                string callbackName = callbackOrder[index];
                if (!rankByCallback.ContainsKey(callbackName))
                {
                    rankByCallback.Add(callbackName, index);
                }
            }

            return rankByCallback;
        }
    }
}
