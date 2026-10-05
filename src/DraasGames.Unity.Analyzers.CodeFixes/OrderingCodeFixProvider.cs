using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DraasGames.Unity.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DraasGames.Unity.Analyzers.CodeFixes
{
    /// <summary>
    /// Provides source-ordering fixes for the two Unity ordering diagnostics.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OrderingCodeFixProvider))]
    [Shared]
    public sealed class OrderingCodeFixProvider : CodeFixProvider
    {
        internal const string GroupSerializedFieldsEquivalenceKey =
            SerializedFieldOrderAnalyzer.DiagnosticId + ":GroupSerializedFields";
        internal const string OrderUnityMethodsEquivalenceKey =
            UnityCallbackOrderAnalyzer.DiagnosticId + ":OrderUnityMethods";

        private static readonly ImmutableArray<string> SupportedDiagnosticIds =
            ImmutableArray.Create(
                SerializedFieldOrderAnalyzer.DiagnosticId,
                UnityCallbackOrderAnalyzer.DiagnosticId);

        public override ImmutableArray<string> FixableDiagnosticIds => SupportedDiagnosticIds;

        public override FixAllProvider GetFixAllProvider()
        {
            return OrderingFixAllProvider.Instance;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            CancellationToken cancellationToken = context.CancellationToken;
            cancellationToken.ThrowIfCancellationRequested();

            ImmutableArray<Diagnostic> diagnostics = context.Diagnostics
                .Where(diagnostic => SupportedDiagnosticIds.Contains(diagnostic.Id))
                .ToImmutableArray();
            if (diagnostics.Length == 0)
            {
                return;
            }

            SyntaxNode? root = await context.Document.GetSyntaxRootAsync(cancellationToken)
                .ConfigureAwait(false);
            SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(cancellationToken)
                .ConfigureAwait(false);
            if (root is null || semanticModel is null)
            {
                return;
            }

            Project project = context.Document.Project;
            AnalyzerOptions analyzerOptions = project.AnalyzerOptions;
            AnalyzerSettings settings = AnalyzerSettings.Load(analyzerOptions, cancellationToken);
            ImmutableArray<PlannedType> plans = OrderingFixPlanner.CreatePlans(
                root,
                semanticModel,
                settings,
                analyzerOptions.AnalyzerConfigOptionsProvider,
                diagnostics,
                cancellationToken);

            foreach (PlannedType plan in plans)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ImmutableArray<Diagnostic> associatedDiagnostics = diagnostics
                    .Where(diagnostic => diagnostic.Id == plan.DiagnosticId
                        && OrderingFixPlanner.FindContainingType(root, diagnostic)?.SpanStart == plan.TypeStart)
                    .ToImmutableArray();
                if (associatedDiagnostics.Length == 0)
                {
                    continue;
                }

                string title;
                string equivalenceKey;
                if (plan.DiagnosticId == SerializedFieldOrderAnalyzer.DiagnosticId)
                {
                    title = "Group serialized fields";
                    equivalenceKey = GroupSerializedFieldsEquivalenceKey;
                }
                else
                {
                    title = "Order Unity methods";
                    equivalenceKey = OrderUnityMethodsEquivalenceKey;
                }

                CodeAction action = CodeAction.Create(
                    title,
                    actionCancellationToken => ApplyPlanAsync(
                        context.Document,
                        root,
                        plan,
                        actionCancellationToken),
                    equivalenceKey);
                context.RegisterCodeFix(action, associatedDiagnostics);
            }
        }

        private static Task<Document> ApplyPlanAsync(
            Document document,
            SyntaxNode root,
            PlannedType plan,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyntaxNode rewrittenRoot = OrderingFixPlanner.RewriteRoot(
                root,
                new[] { plan },
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(document.WithSyntaxRoot(rewrittenRoot));
        }
    }

    internal sealed class OrderingFixAllProvider : FixAllProvider
    {
        internal static readonly OrderingFixAllProvider Instance = new OrderingFixAllProvider();

        private OrderingFixAllProvider()
        {
        }

        public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            CancellationToken cancellationToken = fixAllContext.CancellationToken;
            cancellationToken.ThrowIfCancellationRequested();

            ImmutableHashSet<string> diagnosticIds = GetDiagnosticIds(fixAllContext);
            if (diagnosticIds.Count == 0)
            {
                return null;
            }

            ImmutableArray<Document> documents = GetDocuments(fixAllContext)
                .GroupBy(document => document.Id)
                .Select(group => group.First())
                .ToImmutableArray();
            var documentPlans = ImmutableArray.CreateBuilder<DocumentFixPlan>();

            foreach (Document document in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IEnumerable<Diagnostic>? suppliedDiagnostics =
                    await fixAllContext.GetDocumentDiagnosticsAsync(document).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (suppliedDiagnostics is null)
                {
                    continue;
                }

                ImmutableArray<Diagnostic> diagnostics = suppliedDiagnostics
                    .Where(diagnostic => diagnosticIds.Contains(diagnostic.Id))
                    .ToImmutableArray();
                if (diagnostics.Length == 0)
                {
                    continue;
                }

                SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken)
                    .ConfigureAwait(false);
                SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (root is null || semanticModel is null)
                {
                    continue;
                }

                Project project = document.Project;
                AnalyzerOptions analyzerOptions = project.AnalyzerOptions;
                AnalyzerSettings settings = AnalyzerSettings.Load(analyzerOptions, cancellationToken);
                ImmutableArray<PlannedType> individualPlans = OrderingFixPlanner.CreatePlans(
                    root,
                    semanticModel,
                    settings,
                    analyzerOptions.AnalyzerConfigOptionsProvider,
                    diagnostics,
                    cancellationToken);
                ImmutableDictionary<int, TypePlanSet> plansByType =
                    OrderingFixPlanner.CombinePlans(individualPlans);
                if (plansByType.Count == 0)
                {
                    continue;
                }

                documentPlans.Add(new DocumentFixPlan(document.Id, root, plansByType));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (documentPlans.Count == 0)
            {
                return null;
            }

            ImmutableArray<DocumentFixPlan> plans = documentPlans.ToImmutable();
            string equivalenceKey = fixAllContext.CodeActionEquivalenceKey!;
            return CodeAction.Create(
                "Fix all ordering diagnostics",
                actionCancellationToken => ApplyPlansAsync(
                    fixAllContext.Solution,
                    plans,
                    actionCancellationToken),
                equivalenceKey);
        }

        private static ImmutableHashSet<string> GetDiagnosticIds(FixAllContext fixAllContext)
        {
            var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            if (fixAllContext.CodeActionEquivalenceKey
                == OrderingCodeFixProvider.GroupSerializedFieldsEquivalenceKey)
            {
                builder.Add(SerializedFieldOrderAnalyzer.DiagnosticId);
            }
            else if (fixAllContext.CodeActionEquivalenceKey
                == OrderingCodeFixProvider.OrderUnityMethodsEquivalenceKey)
            {
                builder.Add(UnityCallbackOrderAnalyzer.DiagnosticId);
            }
            else
            {
                return builder.ToImmutable();
            }

            var selected = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            foreach (string diagnosticId in fixAllContext.DiagnosticIds)
            {
                if (builder.Contains(diagnosticId))
                {
                    selected.Add(diagnosticId);
                }
            }

            return selected.ToImmutable();
        }

        private static IEnumerable<Document> GetDocuments(FixAllContext fixAllContext)
        {
            if (fixAllContext.Scope == FixAllScope.Document)
            {
                if (fixAllContext.Document is not null
                    && fixAllContext.Document.Project.Language == LanguageNames.CSharp)
                {
                    yield return fixAllContext.Document;
                }

                yield break;
            }

            if (fixAllContext.Scope == FixAllScope.Project)
            {
                Project? project = fixAllContext.Project;
                if (project is not null && project.Language == LanguageNames.CSharp)
                {
                    foreach (Document document in project.Documents)
                    {
                        yield return document;
                    }
                }

                yield break;
            }

            if (fixAllContext.Scope == FixAllScope.Solution)
            {
                foreach (Project project in fixAllContext.Solution.Projects)
                {
                    if (project.Language != LanguageNames.CSharp)
                    {
                        continue;
                    }

                    foreach (Document document in project.Documents)
                    {
                        yield return document;
                    }
                }

                yield break;
            }

            if (fixAllContext.Document is not null
                && fixAllContext.Document.Project.Language == LanguageNames.CSharp)
            {
                yield return fixAllContext.Document;
            }
        }

        private static Task<Solution> ApplyPlansAsync(
            Solution solution,
            ImmutableArray<DocumentFixPlan> plans,
            CancellationToken cancellationToken)
        {
            foreach (DocumentFixPlan plan in plans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (solution.GetDocument(plan.DocumentId) is null)
                {
                    continue;
                }

                SyntaxNode rewrittenRoot = OrderingFixPlanner.RewriteRoot(
                    plan.Root,
                    plan.PlansByType,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                solution = solution.WithDocumentSyntaxRoot(plan.DocumentId, rewrittenRoot);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(solution);
        }
    }

    internal sealed class DocumentFixPlan
    {
        internal DocumentFixPlan(
            DocumentId documentId,
            SyntaxNode root,
            ImmutableDictionary<int, TypePlanSet> plansByType)
        {
            DocumentId = documentId;
            Root = root;
            PlansByType = plansByType;
        }

        internal DocumentId DocumentId { get; }

        internal SyntaxNode Root { get; }

        internal ImmutableDictionary<int, TypePlanSet> PlansByType { get; }
    }
}
