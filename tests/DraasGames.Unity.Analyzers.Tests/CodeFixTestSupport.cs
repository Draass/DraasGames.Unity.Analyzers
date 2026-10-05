using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DraasGames.Unity.Analyzers.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Composition.Hosting;
using Xunit;

namespace DraasGames.Unity.Analyzers.Tests;

internal static class CodeFixTestSupport
{
    internal const string UnityStubs = @"
namespace UnityEngine
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class SerializeField : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class SerializeReference : System.Attribute { }
    public class Object { }
    public class Component : Object { }
    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { }
    public class Animator : Behaviour { }
    public struct AnimatorStateInfo { }
    public class Camera : Behaviour { }
    public class Collider : Component { }
    public class Collider2D : Component { }
    public class Collision { }
    public class Collision2D { }
    public class ControllerColliderHit { }
    public class Material : Object { }
    public class ParticleSystem : Component { }
    public class Renderer : Component { }
    public class Texture : Object { }
    public class RenderTexture : Texture { }
    public class Transform : Component { }
    public class GUIContent { }
    public class GUIStyle { }
    public class Event { }
    public struct Rect { }
    public struct Vector2 { }
    public struct Vector3 { }
    public struct Quaternion { }
    public struct Ray { }
    public struct RaycastHit { }
    public struct RaycastHit2D { }
    public class StateMachineBehaviour : Object
    {
        public virtual void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { }
    }
    public interface ISerializationCallbackReceiver
    {
        void OnBeforeSerialize();
        void OnAfterDeserialize();
    }
}

namespace UnityEngine.EventSystems
{
    public class BaseEventData { }
    public class PointerEventData : BaseEventData { }
    public class UIBehaviour : UnityEngine.MonoBehaviour { }
    public interface IPointerEnterHandler
    {
        void OnPointerEnter(PointerEventData eventData);
    }
}

namespace UnityEngine.Playables
{
    public struct Playable { }
    public interface INotification { }
    public interface INotificationReceiver
    {
        void OnNotify(Playable origin, INotification notification, object context);
    }
}

namespace UnityEngine.UIElements
{
    public class VisualElement { }
}

namespace UnityEditor
{
    public class SerializedProperty { }
    public class EditorWindow
    {
        public virtual void CreateGUI() { }
    }
    public class Editor
    {
        public virtual void OnInspectorGUI() { }
    }
    public class PropertyDrawer
    {
        public virtual void OnGUI(
            UnityEngine.Rect position,
            SerializedProperty property,
            UnityEngine.GUIContent label) { }
    }
    public class AssetPostprocessor { }
}

namespace Sirenix.Serialization
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class OdinSerializeAttribute : System.Attribute { }
}
";

    internal static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers =
        ImmutableArray.Create<DiagnosticAnalyzer>(
            new SerializedFieldOrderAnalyzer(),
            new UnityCallbackOrderAnalyzer());

    internal static readonly ImmutableArray<MetadataReference> RuntimeReferences = GetRuntimeReferences();

    internal static CodeFixProvider CreateProvider()
    {
        using var container = new ContainerConfiguration()
            .WithAssembly(typeof(OrderingCodeFixProvider).Assembly)
            .CreateContainer();
        return container.GetExports<CodeFixProvider>()
            .Single(provider => provider.GetType() == typeof(OrderingCodeFixProvider));
    }

    internal static Solution CreateSolution(params TestProject[] projects)
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        foreach (var projectDefinition in projects)
        {
            var projectId = ProjectId.CreateNewId(projectDefinition.Name);
            var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                projectDefinition.Name,
                projectDefinition.Name,
                LanguageNames.CSharp,
                filePath: NormalizeProjectPath(projectDefinition.Name, projectDefinition.Name + ".csproj"),
                parseOptions: new CSharpParseOptions(LanguageVersion.CSharp9),
                compilationOptions: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    nullableContextOptions: NullableContextOptions.Disable),
                metadataReferences: RuntimeReferences);
            solution = solution.AddProject(projectInfo);
            solution = solution.AddDocument(
                DocumentId.CreateNewId(projectId, "UnityStubs"),
                "UnityStubs.cs",
                SourceText.From(UnityStubs, Encoding.UTF8),
                    filePath: NormalizeProjectPath(projectDefinition.Name, "UnityStubs.cs"));
            foreach (var source in projectDefinition.Sources)
            {
                var sourcePath = NormalizeProjectPath(projectDefinition.Name, source.FilePath);
                solution = solution.AddDocument(
                    DocumentId.CreateNewId(projectId, source.FilePath),
                    Path.GetFileName(source.FilePath),
                    SourceText.From(source.Text, Encoding.UTF8),
                    filePath: sourcePath);
            }

            foreach (var additional in projectDefinition.AdditionalFiles)
            {
                var additionalPath = NormalizeProjectPath(projectDefinition.Name, additional.FilePath);
                solution = solution.AddAdditionalDocument(
                    DocumentId.CreateNewId(projectId, additional.FilePath),
                    Path.GetFileName(additional.FilePath),
                    SourceText.From(additional.Text, Encoding.UTF8),
                    filePath: additionalPath);
            }

            foreach (var editorConfig in projectDefinition.EditorConfigs)
            {
                var editorConfigPath = NormalizeProjectPath(projectDefinition.Name, editorConfig.FilePath);
                solution = solution.AddAnalyzerConfigDocument(
                    DocumentId.CreateNewId(projectId, editorConfig.FilePath),
                    Path.GetFileName(editorConfig.FilePath),
                    SourceText.From(editorConfig.Text, Encoding.UTF8),
                    filePath: editorConfigPath);
            }
        }

        return solution;
    }

    internal static Document GetDocument(Solution solution, string projectName, string filePath)
    {
        var project = solution.Projects.Single(project => project.Name == projectName);
        return project.Documents.Single(document =>
            string.Equals(document.FilePath, filePath, StringComparison.OrdinalIgnoreCase)
                || document.FilePath?.EndsWith(
                    Path.DirectorySeparatorChar + filePath,
                    StringComparison.OrdinalIgnoreCase) == true
                || document.FilePath?.EndsWith(
                    Path.AltDirectorySeparatorChar + filePath,
                    StringComparison.OrdinalIgnoreCase) == true);
    }

    internal static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);
        Assert.NotNull(compilation);
        var compilationErrors = compilation!
            .GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(
            compilationErrors.Length == 0,
            "Compilation errors:\n" + string.Join("\n", compilationErrors.Select(error => error.ToString())));
        var options = new CompilationWithAnalyzersOptions(
            project.AnalyzerOptions,
            onAnalyzerException: null,
            concurrentAnalysis: false,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false);
        var diagnostics = await compilation!
            .WithAnalyzers(Analyzers, options)
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "AD0001");
        return diagnostics;
    }

    internal static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        Solution solution,
        CancellationToken cancellationToken = default)
    {
        var builder = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var project in solution.Projects)
        {
            builder.AddRange(await GetDiagnosticsAsync(project, cancellationToken));
        }

        return builder.ToImmutable();
    }

    internal static async Task<ImmutableArray<CodeAction>> RegisterActionsAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken = default)
    {
        var actions = ImmutableArray.CreateBuilder<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            cancellationToken);
        await CreateProvider().RegisterCodeFixesAsync(context);
        return actions.ToImmutable();
    }

    internal static async Task<Solution> ApplyActionAsync(
        CodeAction action,
        CancellationToken cancellationToken = default)
    {
        var operations = await action.GetOperationsAsync(cancellationToken);
        var applyChanges = Assert.Single(operations.OfType<ApplyChangesOperation>());
        return applyChanges.ChangedSolution;
    }

    internal static async Task<Solution> ApplyFirstActionAsync(
        Document document,
        Diagnostic diagnostic,
        string? equivalenceKey = null,
        CancellationToken cancellationToken = default)
    {
        var actions = await RegisterActionsAsync(document, diagnostic, cancellationToken);
        Assert.NotEmpty(actions);
        var action = equivalenceKey == null
            ? actions[0]
            : Assert.Single(actions.Where(candidate => candidate.EquivalenceKey == equivalenceKey));
        return await ApplyActionAsync(action, cancellationToken);
    }

    internal static async Task<Solution> ApplyFixAllAsync(
        Solution solution,
        Document triggerDocument,
        string diagnosticId,
        FixAllScope scope,
        string equivalenceKey,
        CancellationToken cancellationToken = default)
    {
        var action = await GetFixAllActionAsync(
            solution,
            triggerDocument,
            diagnosticId,
            scope,
            equivalenceKey,
            cancellationToken);
        Assert.NotNull(action);
        return await ApplyActionAsync(action!, cancellationToken);
    }

    internal static async Task<CodeAction?> GetFixAllActionAsync(
        Solution solution,
        Document triggerDocument,
        string diagnosticId,
        FixAllScope scope,
        string equivalenceKey,
        CancellationToken cancellationToken = default)
    {
        var provider = CreateProvider();
        var fixAllProvider = provider.GetFixAllProvider();
        Assert.NotNull(fixAllProvider);
        var diagnostics = new WorkspaceDiagnosticProvider();
        var context = new FixAllContext(
            triggerDocument,
            provider,
            scope,
            equivalenceKey,
            new[] { diagnosticId },
            diagnostics,
            cancellationToken);
        return await fixAllProvider!.GetFixAsync(context);
    }

    internal static async Task AssertCompilesAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);
        Assert.NotNull(compilation);
        var errors = compilation!.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(
            errors.Length == 0,
            "Compilation errors:\n" + string.Join("\n", errors.Select(error => error.ToString())));
    }

    internal static void AssertOrder(string text, params string[] members)
    {
        var previous = -1;
        foreach (var member in members)
        {
            var index = text.IndexOf(member, StringComparison.Ordinal);
            Assert.True(index >= 0, "Expected output to contain member text: " + member);
            Assert.True(
                index > previous,
                "Expected member order was violated by: " + member + " in output:\n" + text);
            previous = index;
        }
    }

    internal static void AssertAnalyzerClear(
        IEnumerable<Diagnostic> diagnostics,
        string diagnosticId)
    {
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == diagnosticId);
    }

    internal static string? DiagnosticEquivalenceKey(
        IReadOnlyList<CodeAction> actions,
        string title)
    {
        return actions.Single(action => action.Title == title).EquivalenceKey;
    }

    internal sealed class WorkspaceDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            var diagnostics = await GetDiagnosticsAsync(document.Project, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return diagnostics
                .Where(diagnostic => diagnostic.Location.SourceTree?.FilePath == document.FilePath)
                .ToImmutableArray();
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Diagnostic>>(ImmutableArray<Diagnostic>.Empty);
        }

        public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken)
        {
            return await GetDiagnosticsAsync(project, cancellationToken);
        }
    }

    private static ImmutableArray<MetadataReference> GetRuntimeReferences()
    {
        var trustedPlatformAssemblyList = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedPlatformAssemblyList))
        {
            return ImmutableArray.Create<MetadataReference>(
                MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location));
        }

        return trustedPlatformAssemblyList
            .Split(Path.PathSeparator)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static string NormalizeProjectPath(string projectName, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "dgua-codefix-workspaces",
            projectName,
            path));
    }
}

internal sealed class TestProject
{
    internal TestProject(
        string name,
        IEnumerable<TestSource> sources,
        IEnumerable<TestAdditionalFile>? additionalFiles = null,
        IEnumerable<TestAdditionalFile>? editorConfigs = null)
    {
        Name = name;
        Sources = sources.ToImmutableArray();
        AdditionalFiles = (additionalFiles ?? Array.Empty<TestAdditionalFile>()).ToImmutableArray();
        EditorConfigs = (editorConfigs ?? Array.Empty<TestAdditionalFile>()).ToImmutableArray();
    }

    internal string Name { get; }
    internal ImmutableArray<TestSource> Sources { get; }
    internal ImmutableArray<TestAdditionalFile> AdditionalFiles { get; }
    internal ImmutableArray<TestAdditionalFile> EditorConfigs { get; }
}

internal sealed class TestSource
{
    internal TestSource(string filePath, string text)
    {
        FilePath = filePath;
        Text = text;
    }

    internal string FilePath { get; }
    internal string Text { get; }
}

internal sealed class TestAdditionalFile
{
    internal TestAdditionalFile(string filePath, string text)
    {
        FilePath = filePath;
        Text = text;
    }

    internal string FilePath { get; }
    internal string Text { get; }
}
