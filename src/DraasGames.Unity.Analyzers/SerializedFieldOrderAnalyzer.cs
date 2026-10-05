using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DraasGames.Unity.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SerializedFieldOrderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DGUA001";
        private const string Category = "DraasGames.Unity";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Serialized fields should be grouped",
            "Field '{0}' is out of serialized field order; serialized fields must be declared {1} other instance fields",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Keeps explicitly serialized instance fields together according to the configured placement.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(startContext =>
            {
                var settings = AnalyzerSettings.Load(startContext.Options, startContext.CancellationToken);

                startContext.RegisterSyntaxNodeAction(
                    nodeContext => AnalyzeTypeDeclaration(nodeContext, settings),
                    SyntaxKind.ClassDeclaration,
                    SyntaxKind.StructDeclaration,
                    SyntaxKind.RecordDeclaration);
            });
        }

        private static void AnalyzeTypeDeclaration(
            SyntaxNodeAnalysisContext context,
            AnalyzerSettings settings)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var typeDeclaration = (TypeDeclarationSyntax)context.Node;
            var effectiveSettings = settings.ForTree(
                context.Options.AnalyzerConfigOptionsProvider,
                typeDeclaration.SyntaxTree);
            var serializedFieldsFirst = effectiveSettings.SerializedFieldsFirst;
            var seenOrdinaryField = false;
            var seenSerializedField = false;

            foreach (var member in typeDeclaration.Members)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (!(member is FieldDeclarationSyntax fieldDeclaration) || IsStaticOrConst(fieldDeclaration))
                {
                    continue;
                }

                if (fieldDeclaration.Declaration.Variables.Count == 0)
                {
                    continue;
                }

                var firstVariable = fieldDeclaration.Declaration.Variables[0];
                var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(
                    firstVariable,
                    context.CancellationToken) as IFieldSymbol;
                var isSerializedField = fieldSymbol != null && HasSerializedMarker(fieldSymbol);

                if (serializedFieldsFirst)
                {
                    if (isSerializedField)
                    {
                        if (seenOrdinaryField)
                        {
                            ReportViolation(context, firstVariable, serializedFieldsFirst);
                        }
                    }
                    else
                    {
                        seenOrdinaryField = true;
                    }
                }
                else
                {
                    if (isSerializedField)
                    {
                        seenSerializedField = true;
                    }
                    else if (seenSerializedField)
                    {
                        ReportViolation(context, firstVariable, serializedFieldsFirst);
                    }
                }
            }
        }

        private static void ReportViolation(
            SyntaxNodeAnalysisContext context,
            VariableDeclaratorSyntax firstVariable,
            bool serializedFieldsFirst)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                firstVariable.Identifier.GetLocation(),
                firstVariable.Identifier.ValueText,
                serializedFieldsFirst ? "before" : "after"));
        }

        internal static bool IsStaticOrConst(FieldDeclarationSyntax fieldDeclaration)
        {
            foreach (var modifier in fieldDeclaration.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.StaticKeyword) || modifier.IsKind(SyntaxKind.ConstKeyword))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool HasSerializedMarker(IFieldSymbol fieldSymbol)
        {
            foreach (var attribute in fieldSymbol.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass == null || attributeClass.ContainingType != null || attributeClass.Arity != 0)
                {
                    continue;
                }

                var namespaceName = attributeClass.ContainingNamespace.ToDisplayString();
                if (string.Equals(namespaceName, "UnityEngine", StringComparison.Ordinal) &&
                    (string.Equals(attributeClass.Name, "SerializeField", StringComparison.Ordinal) ||
                     string.Equals(attributeClass.Name, "SerializeReference", StringComparison.Ordinal)))
                {
                    return true;
                }

                if (string.Equals(namespaceName, "Sirenix.Serialization", StringComparison.Ordinal) &&
                    string.Equals(attributeClass.Name, "OdinSerializeAttribute", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
