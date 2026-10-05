using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using DraasGames.Unity.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace DraasGames.Unity.Analyzers.CodeFixes
{
    internal sealed class PlannedType
    {
        internal PlannedType(string diagnosticId, int typeStart, TypePlanSet plan)
        {
            DiagnosticId = diagnosticId;
            TypeStart = typeStart;
            Plan = plan;
        }

        internal string DiagnosticId { get; }

        internal int TypeStart { get; }

        internal TypePlanSet Plan { get; }
    }

    internal sealed class TypePlanSet
    {
        internal TypePlanSet(
            int typeStart,
            MemberPermutationPlan? fieldPlan,
            MemberPermutationPlan? methodPlan)
        {
            TypeStart = typeStart;
            FieldPlan = fieldPlan;
            MethodPlan = methodPlan;
        }

        internal int TypeStart { get; }

        internal MemberPermutationPlan? FieldPlan { get; }

        internal MemberPermutationPlan? MethodPlan { get; }

        internal TypePlanSet WithFieldPlan(MemberPermutationPlan fieldPlan)
        {
            return new TypePlanSet(TypeStart, fieldPlan, MethodPlan);
        }

        internal TypePlanSet WithMethodPlan(MemberPermutationPlan methodPlan)
        {
            return new TypePlanSet(TypeStart, FieldPlan, methodPlan);
        }
    }

    internal sealed class MemberPermutationPlan
    {
        internal MemberPermutationPlan(
            ImmutableArray<int> newMemberIndices,
            ImmutableArray<int> targetMemberPositions)
        {
            NewMemberIndices = newMemberIndices;
            TargetMemberPositions = targetMemberPositions;
        }

        internal ImmutableArray<int> NewMemberIndices { get; }

        internal ImmutableArray<int> TargetMemberPositions { get; }
    }

    internal static class OrderingFixPlanner
    {
        internal static ImmutableArray<PlannedType> CreatePlans(
            SyntaxNode root,
            SemanticModel semanticModel,
            AnalyzerSettings settings,
            AnalyzerConfigOptionsProvider optionsProvider,
            IEnumerable<Diagnostic> diagnostics,
            CancellationToken cancellationToken)
        {
            var candidates = new Dictionary<string, TypeDeclarationSyntax>(StringComparer.Ordinal);
            foreach (Diagnostic diagnostic in diagnostics)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (diagnostic.Id != SerializedFieldOrderAnalyzer.DiagnosticId
                    && diagnostic.Id != UnityCallbackOrderAnalyzer.DiagnosticId)
                {
                    continue;
                }

                TypeDeclarationSyntax? type = FindContainingType(root, diagnostic);
                if (type is null)
                {
                    continue;
                }

                string key = diagnostic.Id + "\0"
                    + type.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture);
                candidates[key] = type;
            }

            var plans = ImmutableArray.CreateBuilder<PlannedType>();
            foreach (KeyValuePair<string, TypeDeclarationSyntax> candidate in candidates
                         .OrderBy(pair => pair.Value.SpanStart)
                         .ThenBy(pair => pair.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                int separator = candidate.Key.IndexOf('\0');
                string diagnosticId = candidate.Key.Substring(0, separator);
                TypeDeclarationSyntax type = candidate.Value;
                AnalyzerSettings effectiveSettings = settings.ForTree(
                    optionsProvider,
                    type.SyntaxTree);
                MemberPermutationPlan? permutation = diagnosticId == SerializedFieldOrderAnalyzer.DiagnosticId
                    ? TryCreateFieldPlan(type, semanticModel, effectiveSettings, cancellationToken)
                    : TryCreateMethodPlan(type, semanticModel, effectiveSettings, cancellationToken);
                if (permutation is null)
                {
                    continue;
                }

                var plan = diagnosticId == SerializedFieldOrderAnalyzer.DiagnosticId
                    ? new TypePlanSet(type.SpanStart, permutation, null)
                    : new TypePlanSet(type.SpanStart, null, permutation);
                plans.Add(new PlannedType(diagnosticId, type.SpanStart, plan));
            }

            return plans.ToImmutable();
        }

        internal static ImmutableDictionary<int, TypePlanSet> CombinePlans(
            ImmutableArray<PlannedType> individualPlans)
        {
            var result = ImmutableDictionary.CreateBuilder<int, TypePlanSet>();
            foreach (PlannedType individualPlan in individualPlans)
            {
                if (!result.TryGetValue(individualPlan.TypeStart, out TypePlanSet? existing))
                {
                    result.Add(individualPlan.TypeStart, individualPlan.Plan);
                    continue;
                }

                TypePlanSet combined = existing;
                if (individualPlan.Plan.FieldPlan is not null)
                {
                    combined = combined.WithFieldPlan(individualPlan.Plan.FieldPlan);
                }

                if (individualPlan.Plan.MethodPlan is not null)
                {
                    combined = combined.WithMethodPlan(individualPlan.Plan.MethodPlan);
                }

                result[individualPlan.TypeStart] = combined;
            }

            return result.ToImmutable();
        }

        internal static TypeDeclarationSyntax? FindContainingType(SyntaxNode root, Diagnostic diagnostic)
        {
            SyntaxTree? diagnosticTree = diagnostic.Location.SourceTree;
            if (!diagnostic.Location.IsInSource || diagnosticTree is null)
            {
                return null;
            }

            if (diagnosticTree != root.SyntaxTree
                && (string.IsNullOrEmpty(diagnosticTree.FilePath)
                    || string.IsNullOrEmpty(root.SyntaxTree.FilePath)
                    || !string.Equals(
                        diagnosticTree.FilePath,
                        root.SyntaxTree.FilePath,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            TextSpan span = diagnostic.Location.SourceSpan;
            if (span.Start < 0 || span.End > root.FullSpan.End)
            {
                return null;
            }

            SyntaxNode node = root.FindNode(span, getInnermostNodeForTie: true);
            return node.AncestorsAndSelf()
                .OfType<TypeDeclarationSyntax>()
                .FirstOrDefault();
        }

        internal static SyntaxNode RewriteRoot(
            SyntaxNode root,
            IEnumerable<PlannedType> individualPlans,
            CancellationToken cancellationToken)
        {
            var plansByType = new Dictionary<int, TypePlanSet>();
            foreach (PlannedType individualPlan in individualPlans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!plansByType.TryGetValue(individualPlan.TypeStart, out TypePlanSet? existing))
                {
                    plansByType.Add(individualPlan.TypeStart, individualPlan.Plan);
                    continue;
                }

                TypePlanSet combined = existing;
                if (individualPlan.Plan.FieldPlan is not null)
                {
                    combined = combined.WithFieldPlan(individualPlan.Plan.FieldPlan);
                }

                if (individualPlan.Plan.MethodPlan is not null)
                {
                    combined = combined.WithMethodPlan(individualPlan.Plan.MethodPlan);
                }

                plansByType[individualPlan.TypeStart] = combined;
            }

            return RewriteRoot(root, ToImmutablePlans(plansByType), cancellationToken);
        }

        internal static SyntaxNode RewriteRoot(
            SyntaxNode root,
            ImmutableDictionary<int, TypePlanSet> plansByType,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rewriter = new OrderingSyntaxRewriter(plansByType, cancellationToken);
            SyntaxNode? rewrittenRoot = rewriter.Visit(root);
            cancellationToken.ThrowIfCancellationRequested();
            return rewrittenRoot ?? root;
        }

        private static ImmutableDictionary<int, TypePlanSet> ToImmutablePlans(
            Dictionary<int, TypePlanSet> plansByType)
        {
            var builder = ImmutableDictionary.CreateBuilder<int, TypePlanSet>();
            foreach (KeyValuePair<int, TypePlanSet> plan in plansByType)
            {
                builder.Add(plan.Key, plan.Value);
            }

            return builder.ToImmutable();
        }

        private static MemberPermutationPlan? TryCreateFieldPlan(
            TypeDeclarationSyntax type,
            SemanticModel semanticModel,
            AnalyzerSettings settings,
            CancellationToken cancellationToken)
        {
            if (IsUnsafeType(type))
            {
                return null;
            }

            INamedTypeSymbol? typeSymbol = semanticModel.GetDeclaredSymbol(type, cancellationToken)
                as INamedTypeSymbol;
            if (typeSymbol is null
                || typeSymbol.TypeKind == TypeKind.Struct
                || HasStructLayout(typeSymbol))
            {
                return null;
            }

            MemberDeclarationSyntax[] members = type.Members.ToArray();
            var fields = new List<FieldSlot>();
            for (int memberIndex = 0; memberIndex < members.Length; memberIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!(members[memberIndex] is FieldDeclarationSyntax field)
                    || SerializedFieldOrderAnalyzer.IsStaticOrConst(field)
                    || field.Declaration.Variables.Count == 0)
                {
                    continue;
                }

                VariableDeclaratorSyntax firstVariable = field.Declaration.Variables[0];
                IFieldSymbol? fieldSymbol = semanticModel.GetDeclaredSymbol(firstVariable, cancellationToken)
                    as IFieldSymbol;
                bool isSerialized = fieldSymbol is not null
                    && SerializedFieldOrderAnalyzer.HasSerializedMarker(fieldSymbol);
                fields.Add(new FieldSlot(memberIndex, isSerialized));
            }

            if (fields.Count < 2)
            {
                return null;
            }

            var orderedFields = settings.SerializedFieldsFirst
                ? fields.Where(field => field.IsSerialized)
                    .Concat(fields.Where(field => !field.IsSerialized))
                    .ToList()
                : fields.Where(field => !field.IsSerialized)
                    .Concat(fields.Where(field => field.IsSerialized))
                    .ToList();

            int[] permutation = CreateIdentityPermutation(members.Length);
            for (int slot = 0; slot < fields.Count; slot++)
            {
                permutation[fields[slot].MemberIndex] = orderedFields[slot].MemberIndex;
            }

            if (IsIdentity(permutation))
            {
                return null;
            }

            if (!PreservesInitializerOrder(members, permutation, cancellationToken))
            {
                return null;
            }

            return new MemberPermutationPlan(
                permutation.ToImmutableArray(),
                fields.Select(field => field.MemberIndex).ToImmutableArray());
        }

        private static MemberPermutationPlan? TryCreateMethodPlan(
            TypeDeclarationSyntax type,
            SemanticModel semanticModel,
            AnalyzerSettings settings,
            CancellationToken cancellationToken)
        {
            if (!(type is ClassDeclarationSyntax) || IsUnsafeType(type))
            {
                return null;
            }

            INamedTypeSymbol? typeSymbol = semanticModel.GetDeclaredSymbol(type, cancellationToken)
                as INamedTypeSymbol;
            if (typeSymbol is null || !UnityCallbackClassifier.IsUnityType(typeSymbol))
            {
                return null;
            }

            MemberDeclarationSyntax[] members = type.Members.ToArray();
            var methods = new List<MethodSlot>();
            for (int memberIndex = 0; memberIndex < members.Length; memberIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!(members[memberIndex] is MethodDeclarationSyntax method))
                {
                    continue;
                }

                IMethodSymbol? methodSymbol = semanticModel.GetDeclaredSymbol(method, cancellationToken)
                    as IMethodSymbol;
                bool isCallback = methodSymbol is not null
                    && UnityCallbackClassifier.IsUnityMethod(methodSymbol, cancellationToken);
                methods.Add(new MethodSlot(memberIndex, isCallback, method.Identifier.ValueText));
            }

            if (methods.Count < 2)
            {
                return null;
            }

            var rankByCallback = CreateRankMap(settings.CallbackOrder);
            foreach (MethodSlot method in methods)
            {
                cancellationToken.ThrowIfCancellationRequested();
                method.Rank = method.IsCallback
                    && rankByCallback.TryGetValue(method.Name, out int rank)
                    ? rank
                    : -1;
            }

            List<MethodSlot> orderedMethods;
            if (settings.CallbacksBeforeOtherMethods)
            {
                orderedMethods = methods.Where(method => method.IsCallback)
                    .Concat(methods.Where(method => !method.IsCallback))
                    .ToList();
            }
            else
            {
                orderedMethods = methods.ToList();
            }

            List<int> rankedSlots = orderedMethods
                .Select((method, slot) => new { method, slot })
                .Where(item => item.method.Rank >= 0)
                .Select(item => item.slot)
                .ToList();
            List<MethodSlot> rankedMethods = orderedMethods
                .Where(method => method.Rank >= 0)
                .OrderBy(method => method.Rank)
                .ThenBy(method => method.MemberIndex)
                .ToList();
            for (int index = 0; index < rankedSlots.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                orderedMethods[rankedSlots[index]] = rankedMethods[index];
            }

            int[] permutation = CreateIdentityPermutation(members.Length);
            for (int slot = 0; slot < methods.Count; slot++)
            {
                permutation[methods[slot].MemberIndex] = orderedMethods[slot].MemberIndex;
            }

            if (IsIdentity(permutation))
            {
                return null;
            }

            return new MemberPermutationPlan(
                permutation.ToImmutableArray(),
                methods.Select(method => method.MemberIndex).ToImmutableArray());
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

        private static bool PreservesInitializerOrder(
            MemberDeclarationSyntax[] members,
            int[] permutation,
            CancellationToken cancellationToken)
        {
            List<SyntaxNode> originalInitializers = CollectInitializers(
                members,
                Enumerable.Range(0, members.Length),
                cancellationToken);
            List<SyntaxNode> reorderedInitializers = CollectInitializers(
                members,
                permutation,
                cancellationToken);
            if (originalInitializers.Count != reorderedInitializers.Count)
            {
                return false;
            }

            for (int index = 0; index < originalInitializers.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!ReferenceEquals(originalInitializers[index], reorderedInitializers[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<SyntaxNode> CollectInitializers(
            MemberDeclarationSyntax[] members,
            IEnumerable<int> memberIndices,
            CancellationToken cancellationToken)
        {
            var initializers = new List<SyntaxNode>();
            foreach (int memberIndex in memberIndices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MemberDeclarationSyntax member = members[memberIndex];
                if (member is FieldDeclarationSyntax field)
                {
                    if (SerializedFieldOrderAnalyzer.IsStaticOrConst(field))
                    {
                        continue;
                    }

                    foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                    {
                        if (variable.Initializer is not null)
                        {
                            initializers.Add(variable.Initializer.Value);
                        }
                    }

                    continue;
                }

                if (member is PropertyDeclarationSyntax property
                    && !property.Modifiers.Any(SyntaxKind.StaticKeyword)
                    && property.Initializer is not null)
                {
                    initializers.Add(property.Initializer.Value);
                    continue;
                }

                if (member is EventFieldDeclarationSyntax eventField
                    && !eventField.Modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    foreach (VariableDeclaratorSyntax variable in eventField.Declaration.Variables)
                    {
                        if (variable.Initializer is not null)
                        {
                            initializers.Add(variable.Initializer.Value);
                        }
                    }
                }
            }

            return initializers;
        }

        private static bool IsUnsafeType(TypeDeclarationSyntax type)
        {
            if (type.ContainsDiagnostics)
            {
                return true;
            }

            foreach (SyntaxTrivia trivia in type.DescendantTrivia(descendIntoTrivia: true))
            {
                if (trivia.GetStructure() is DirectiveTriviaSyntax)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasStructLayout(INamedTypeSymbol typeSymbol)
        {
            foreach (AttributeData attribute in typeSymbol.GetAttributes())
            {
                INamedTypeSymbol? attributeClass = attribute.AttributeClass;
                if (attributeClass is null
                    || attributeClass.ContainingNamespace.ToDisplayString()
                        != "System.Runtime.InteropServices"
                    || (attributeClass.Name != "StructLayoutAttribute"
                        && attributeClass.Name != "StructLayout"))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private sealed class FieldSlot
        {
            internal FieldSlot(int memberIndex, bool isSerialized)
            {
                MemberIndex = memberIndex;
                IsSerialized = isSerialized;
            }

            internal int MemberIndex { get; }

            internal bool IsSerialized { get; }
        }

        private sealed class MethodSlot
        {
            internal MethodSlot(int memberIndex, bool isCallback, string name)
            {
                MemberIndex = memberIndex;
                IsCallback = isCallback;
                Name = name;
            }

            internal int MemberIndex { get; }

            internal bool IsCallback { get; }

            internal string Name { get; }

            internal int Rank { get; set; }
        }

        private static int[] CreateIdentityPermutation(int length)
        {
            var permutation = new int[length];
            for (int index = 0; index < length; index++)
            {
                permutation[index] = index;
            }

            return permutation;
        }

        private static bool IsIdentity(int[] permutation)
        {
            for (int index = 0; index < permutation.Length; index++)
            {
                if (permutation[index] != index)
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class OrderingSyntaxRewriter : CSharpSyntaxRewriter
        {
            private readonly ImmutableDictionary<int, TypePlanSet> plansByType;
            private readonly CancellationToken cancellationToken;

            internal OrderingSyntaxRewriter(
                ImmutableDictionary<int, TypePlanSet> plansByType,
                CancellationToken cancellationToken)
            {
                this.plansByType = plansByType;
                this.cancellationToken = cancellationToken;
            }

            public override SyntaxNode? Visit(SyntaxNode? node)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (node is TypeDeclarationSyntax type
                    && plansByType.TryGetValue(type.SpanStart, out TypePlanSet? plan))
                {
                    return RewriteType(type, plan);
                }

                return base.Visit(node);
            }

            private TypeDeclarationSyntax RewriteType(TypeDeclarationSyntax type, TypePlanSet plan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MemberDeclarationSyntax[] originalMembers = type.Members.ToArray();
                var rewrittenMembers = new MemberDeclarationSyntax[originalMembers.Length];
                for (int index = 0; index < originalMembers.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    rewrittenMembers[index] = Visit(originalMembers[index])
                        as MemberDeclarationSyntax
                        ?? originalMembers[index];
                }

                int[] finalOrder = CreateIdentityPermutation(originalMembers.Length);
                ApplyPermutation(finalOrder, plan.FieldPlan);
                ApplyPermutation(finalOrder, plan.MethodPlan);

                var orderedMembers = new MemberDeclarationSyntax[rewrittenMembers.Length];
                for (int destination = 0; destination < orderedMembers.Length; destination++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    orderedMembers[destination] = rewrittenMembers[finalOrder[destination]];
                }

                return type.WithMembers(SyntaxFactory.List(orderedMembers));
            }

            private static void ApplyPermutation(
                int[] finalOrder,
                MemberPermutationPlan? permutation)
            {
                if (permutation is null)
                {
                    return;
                }

                foreach (int destination in permutation.TargetMemberPositions)
                {
                    finalOrder[destination] = permutation.NewMemberIndices[destination];
                }
            }
        }
    }
}
