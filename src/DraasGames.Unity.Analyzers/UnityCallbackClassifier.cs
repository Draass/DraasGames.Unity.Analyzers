using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace DraasGames.Unity.Analyzers
{
    /// <summary>
    /// Recognizes Unity classes and Unity messages from Roslyn symbols only.  The classifier
    /// deliberately does not reference Unity assemblies: Unity projects can supply their own
    /// assembly symbols while analyzer unit tests can provide small semantic stubs.
    /// </summary>
    internal static class UnityCallbackClassifier
    {
        private const string UnityEngineNamespace = "UnityEngine";
        private const string UnityEditorNamespace = "UnityEditor";
        private const string UnityObject = "UnityEngine.Object";

        internal static bool IsUnityType(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol is null || typeSymbol.TypeKind != TypeKind.Class)
            {
                return false;
            }

            for (INamedTypeSymbol? current = typeSymbol; current is not null; current = current.BaseType)
            {
                if (IsUnityObjectType(current)
                    || UnityMessageCatalog.IsKnownHost(current)
                    || (!SymbolEqualityComparer.Default.Equals(current, typeSymbol)
                        && IsUnityNamespaceType(current)))
                {
                    return true;
                }
            }

            foreach (INamedTypeSymbol interfaceType in typeSymbol.AllInterfaces)
            {
                if (IsUnityInterface(interfaceType))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsUnityMethod(
            IMethodSymbol methodSymbol,
            System.Threading.CancellationToken cancellationToken)
        {
            if (methodSymbol is null)
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if ((methodSymbol.MethodKind != MethodKind.Ordinary
                && methodSymbol.MethodKind != MethodKind.ExplicitInterfaceImplementation)
                || methodSymbol.ContainingType is null
                || !IsUnityType(methodSymbol.ContainingType))
            {
                return false;
            }

            // Overrides and interface implementations are semantic contracts.  This check is
            // intentionally performed before name/catalog matching so an actual Unity contract
            // can use a modern or package-provided message absent from the static table.
            if (HasUnityOverride(methodSymbol, cancellationToken)
                || ImplementsUnityInterface(methodSymbol, cancellationToken))
            {
                return true;
            }

            return MatchesCatalogMessage(methodSymbol, cancellationToken);
        }

        private static bool MatchesCatalogMessage(
            IMethodSymbol methodSymbol,
            System.Threading.CancellationToken cancellationToken)
        {
            if (methodSymbol.IsAbstract
                || methodSymbol.IsGenericMethod
                || methodSymbol.ReturnsByRef
                || methodSymbol.ReturnsByRefReadonly)
            {
                return false;
            }

            foreach (INamedTypeSymbol host in EnumerateBaseTypes(methodSymbol.ContainingType, cancellationToken))
            {
                string hostMetadataName = GetMetadataName(host);
                foreach (UnityMethodSignature message in UnityMessageCatalog.GetMethods(
                    hostMetadataName,
                    methodSymbol.Name))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (message.IsStatic != methodSymbol.IsStatic
                        || !MatchesReturnType(methodSymbol, message.ReturnKind)
                        || !MatchesParameters(methodSymbol, message))
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        private static bool MatchesParameters(
            IMethodSymbol methodSymbol,
            UnityMethodSignature message)
        {
            int actualCount = methodSymbol.Parameters.Length;
            int expectedCount = message.Parameters.Length;
            if (actualCount > expectedCount
                || (!message.AllowOmittedTrailingParameters && actualCount != expectedCount))
            {
                return false;
            }

            for (int index = 0; index < actualCount; index++)
            {
                IParameterSymbol actual = methodSymbol.Parameters[index];
                UnityParameterSignature expected = message.Parameters[index];
                if (actual.RefKind != expected.RefKind
                    || !MatchesType(actual.Type, expected.Type))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesReturnType(
            IMethodSymbol methodSymbol,
            UnityReturnKind returnKind)
        {
            switch (returnKind)
            {
                case UnityReturnKind.Void:
                    return methodSymbol.ReturnsVoid;
                case UnityReturnKind.Coroutine:
                    return methodSymbol.ReturnsVoid || IsCoroutineReturn(methodSymbol.ReturnType);
                case UnityReturnKind.Boolean:
                    return IsSpecialType(methodSymbol.ReturnType, SpecialType.System_Boolean);
                case UnityReturnKind.String:
                    return IsSpecialType(methodSymbol.ReturnType, SpecialType.System_String);
                case UnityReturnKind.Material:
                    return MatchesType(methodSymbol.ReturnType, UnityTypeSignature.Named("UnityEngine.Material"));
                case UnityReturnKind.Bounds:
                    return MatchesType(methodSymbol.ReturnType, UnityTypeSignature.Named("UnityEngine.Bounds"));
                case UnityReturnKind.VisualElement:
                    return MatchesType(
                        methodSymbol.ReturnType,
                        UnityTypeSignature.Named("UnityEngine.UIElements.VisualElement"));
                case UnityReturnKind.StringArray:
                    return MatchesType(
                        methodSymbol.ReturnType,
                        UnityTypeSignature.Named("System.String[]"));
                default:
                    return false;
            }
        }

        private static bool IsCoroutineReturn(ITypeSymbol returnType)
        {
            if (IsSpecialType(returnType, SpecialType.System_Collections_IEnumerator))
            {
                return true;
            }

            if (!(returnType is INamedTypeSymbol namedReturnType))
            {
                return false;
            }

            string metadataName = GetMetadataName(namedReturnType.OriginalDefinition);
            if (metadataName == "UnityEngine.Awaitable")
            {
                return namedReturnType.TypeArguments.Length == 0;
            }

            if (metadataName == "Cysharp.Threading.Tasks.UniTaskVoid")
            {
                return namedReturnType.TypeArguments.Length == 0;
            }

            if (namedReturnType.TypeArguments.Length == 1
                && (metadataName == "UnityEngine.Awaitable`1"
                    || metadataName == "Cysharp.Threading.Tasks.UniTask`1"))
            {
                return IsSpecialType(
                    namedReturnType.TypeArguments[0],
                    SpecialType.System_Collections_IEnumerator);
            }

            return false;
        }

        private static bool MatchesType(ITypeSymbol actual, UnityTypeSignature expected)
        {
            if (expected.IsArray)
            {
                return actual is IArrayTypeSymbol array
                    && array.Rank == 1
                    && MatchesType(array.ElementType, UnityTypeSignature.Named(expected.MetadataName));
            }

            if (!(actual is INamedTypeSymbol named))
            {
                return false;
            }

            INamedTypeSymbol definition = named.OriginalDefinition;
            if (!string.Equals(GetMetadataName(definition), expected.MetadataName, StringComparison.Ordinal)
                || definition.Arity != expected.TypeArguments.Length)
            {
                return false;
            }

            for (int index = 0; index < expected.TypeArguments.Length; index++)
            {
                if (!MatchesType(named.TypeArguments[index], expected.TypeArguments[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSpecialType(ITypeSymbol typeSymbol, SpecialType specialType)
        {
            return typeSymbol.SpecialType == specialType;
        }

        private static bool HasUnityOverride(
            IMethodSymbol methodSymbol,
            System.Threading.CancellationToken cancellationToken)
        {
            for (IMethodSymbol? current = methodSymbol.OverriddenMethod;
                current is not null;
                current = current.OverriddenMethod)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsUnityNamespaceType(current.ContainingType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ImplementsUnityInterface(
            IMethodSymbol methodSymbol,
            System.Threading.CancellationToken cancellationToken)
        {
            foreach (IMethodSymbol explicitImplementation in methodSymbol.ExplicitInterfaceImplementations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsUnityInterface(explicitImplementation.ContainingType))
                {
                    return true;
                }
            }

            foreach (INamedTypeSymbol interfaceType in methodSymbol.ContainingType.AllInterfaces)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsUnityInterface(interfaceType))
                {
                    continue;
                }

                foreach (ISymbol interfaceMember in interfaceType.GetMembers(methodSymbol.Name))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!(interfaceMember is IMethodSymbol interfaceMethod))
                    {
                        continue;
                    }

                    ISymbol? implementation = methodSymbol.ContainingType.FindImplementationForInterfaceMember(
                        interfaceMethod);
                    if (MethodsEqual(implementation, methodSymbol))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool MethodsEqual(ISymbol? left, IMethodSymbol right)
        {
            if (!(left is IMethodSymbol leftMethod))
            {
                return false;
            }

            return SymbolEqualityComparer.Default.Equals(leftMethod, right)
                || SymbolEqualityComparer.Default.Equals(leftMethod.OriginalDefinition, right.OriginalDefinition);
        }

        private static IEnumerable<INamedTypeSymbol> EnumerateBaseTypes(
            INamedTypeSymbol typeSymbol,
            System.Threading.CancellationToken cancellationToken)
        {
            for (INamedTypeSymbol? current = typeSymbol; current is not null; current = current.BaseType)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return current;
            }
        }

        private static bool IsUnityObjectType(INamedTypeSymbol typeSymbol)
        {
            return string.Equals(GetMetadataName(typeSymbol), UnityObject, StringComparison.Ordinal);
        }

        private static bool IsUnityInterface(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.TypeKind == TypeKind.Interface
                && IsUnityNamespaceType(typeSymbol);
        }

        private static bool IsUnityNamespaceType(INamedTypeSymbol? typeSymbol)
        {
            return typeSymbol is not null && IsUnityNamespace(typeSymbol.ContainingNamespace);
        }

        private static bool IsUnityNamespace(INamespaceSymbol? namespaceSymbol)
        {
            if (namespaceSymbol is null)
            {
                return false;
            }

            string namespaceName = namespaceSymbol.ToDisplayString();
            return namespaceName.Equals(UnityEngineNamespace, StringComparison.Ordinal)
                || namespaceName.StartsWith(UnityEngineNamespace + ".", StringComparison.Ordinal)
                || namespaceName.Equals(UnityEditorNamespace, StringComparison.Ordinal)
                || namespaceName.StartsWith(UnityEditorNamespace + ".", StringComparison.Ordinal);
        }

        private static string GetMetadataName(INamedTypeSymbol typeSymbol)
        {
            var typeNames = new List<string>();
            for (INamedTypeSymbol? current = typeSymbol;
                current is not null;
                current = current.ContainingType)
            {
                typeNames.Add(current.MetadataName);
            }

            typeNames.Reverse();
            string namespaceName = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            string typeName = string.Join(".", typeNames);
            return namespaceName.Length == 0 ? typeName : namespaceName + "." + typeName;
        }
    }
}
