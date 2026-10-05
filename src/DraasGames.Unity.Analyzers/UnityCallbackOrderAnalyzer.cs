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
            "Unity callback '{0}' appears after preceding callback '{1}', but the configured order requires '{0}' before it",
            "DraasGames.Unity",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Unity callback methods should follow the configured callback order within each type declaration.");

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

            UnityTypeKind unityTypeKind = GetUnityTypeKind(
                typeSymbol,
                context.CancellationToken);
            if (unityTypeKind == UnityTypeKind.None)
            {
                return;
            }

            AnalyzerSettings effectiveSettings = settings.ForTree(
                context.Options.AnalyzerConfigOptionsProvider,
                typeDeclaration.SyntaxTree);
            Dictionary<string, int> rankByCallback = CreateRankMap(effectiveSettings.CallbackOrder);
            if (rankByCallback.Count == 0)
            {
                return;
            }

            INamedTypeSymbol? coroutineType = unityTypeKind == UnityTypeKind.MonoBehaviour
                ? context.SemanticModel.Compilation.GetTypeByMetadataName("System.Collections.IEnumerator")
                : null;

            int highestRank = -1;
            string? highestCallbackName = null;

            foreach (MemberDeclarationSyntax member in typeDeclaration.Members)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (member is not MethodDeclarationSyntax methodDeclaration)
                {
                    continue;
                }

                string callbackName = methodDeclaration.Identifier.ValueText;
                if (!rankByCallback.TryGetValue(callbackName, out int callbackRank))
                {
                    continue;
                }

                if (!IsCallbackNameAllowed(unityTypeKind, callbackName))
                {
                    continue;
                }

                IMethodSymbol? methodSymbol = context.SemanticModel.GetDeclaredSymbol(
                    methodDeclaration,
                    context.CancellationToken);
                if (methodSymbol is null || !IsValidCallbackMethod(
                        methodDeclaration,
                        methodSymbol,
                        unityTypeKind,
                        callbackName,
                        coroutineType))
                {
                    continue;
                }

                if (highestRank >= 0 && callbackRank < highestRank)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Rule,
                        methodDeclaration.Identifier.GetLocation(),
                        callbackName,
                        highestCallbackName!));
                }

                if (callbackRank > highestRank)
                {
                    highestRank = callbackRank;
                    highestCallbackName = callbackName;
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

        private static UnityTypeKind GetUnityTypeKind(
            INamedTypeSymbol typeSymbol,
            CancellationToken cancellationToken)
        {
            for (INamedTypeSymbol? current = typeSymbol; current is not null; current = current.BaseType)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsUnityType(current, "MonoBehaviour"))
                {
                    return UnityTypeKind.MonoBehaviour;
                }

                if (IsUnityType(current, "ScriptableObject"))
                {
                    return UnityTypeKind.ScriptableObject;
                }
            }

            return UnityTypeKind.None;
        }

        private static bool IsUnityType(INamedTypeSymbol typeSymbol, string typeName)
        {
            return string.Equals(typeSymbol.Name, typeName, StringComparison.Ordinal)
                && typeSymbol.Arity == 0
                && typeSymbol.ContainingType is null
                && string.Equals(
                    typeSymbol.ContainingNamespace?.ToDisplayString(),
                    "UnityEngine",
                    StringComparison.Ordinal);
        }

        private static bool IsCallbackNameAllowed(UnityTypeKind unityTypeKind, string callbackName)
        {
            if (unityTypeKind == UnityTypeKind.MonoBehaviour)
            {
                return callbackName == "Reset"
                    || callbackName == "OnValidate"
                    || callbackName == "Awake"
                    || callbackName == "OnEnable"
                    || callbackName == "Start"
                    || callbackName == "FixedUpdate"
                    || callbackName == "Update"
                    || callbackName == "LateUpdate"
                    || callbackName == "OnDisable"
                    || callbackName == "OnDestroy";
            }

            return unityTypeKind == UnityTypeKind.ScriptableObject
                && (callbackName == "OnValidate"
                    || callbackName == "Awake"
                    || callbackName == "OnEnable"
                    || callbackName == "OnDisable"
                    || callbackName == "OnDestroy");
        }

        private static bool IsValidCallbackMethod(
            MethodDeclarationSyntax methodDeclaration,
            IMethodSymbol methodSymbol,
            UnityTypeKind unityTypeKind,
            string callbackName,
            INamedTypeSymbol? coroutineType)
        {
            if (methodDeclaration.Body is null && methodDeclaration.ExpressionBody is null)
            {
                return false;
            }

            if (methodSymbol.MethodKind != MethodKind.Ordinary
                || methodSymbol.IsStatic
                || methodSymbol.IsAbstract
                || methodSymbol.IsGenericMethod
                || methodSymbol.Parameters.Length != 0
                || methodSymbol.ReturnsByRef
                || methodSymbol.ReturnsByRefReadonly)
            {
                return false;
            }

            if (methodSymbol.ReturnsVoid)
            {
                return true;
            }

            return unityTypeKind == UnityTypeKind.MonoBehaviour
                && callbackName == "Start"
                && coroutineType is not null
                && SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, coroutineType);
        }

        private enum UnityTypeKind
        {
            None,
            MonoBehaviour,
            ScriptableObject
        }
    }
}
