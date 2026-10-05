using System.Collections.Immutable;
using System.Threading;
using DraasGames.Unity.Analyzers.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace DraasGames.Unity.Analyzers.Tests;

public sealed class OrderingCodeFixTests
{
    [Fact]
    public async Task CallbackFix_GroupsAndOrdersMethods()
    {
        var solution = CodeFixTestSupport.CreateSolution(
            new TestProject(
                "Callbacks",
                new[]
                {
                    Source("Callbacks.cs", @"
public sealed class Example : MonoBehaviour, ISerializationCallbackReceiver
{
    public void Helper() { var value = 1; }
    public void OnBeforeSerialize() { }
    public void Update() { var value = 2; }
    public void Awake() { var value = 3; }
    public void OnAfterDeserialize() { }
}
"),
                    Source("Overrides.cs", @"
public sealed class StateCallbacks : StateMachineBehaviour
{
    public void Helper() { }
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) { }
}
")
                }));

        var project = solution.Projects.Single();
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(project);
        var callbackDiagnostic = Find(diagnostics, "DGUA002", "Callbacks.cs", "OnBeforeSerialize");
        var overrideDiagnostic = Find(diagnostics, "DGUA002", "Overrides.cs", "OnStateEnter");

        var callbackDocument = CodeFixTestSupport.GetDocument(solution, "Callbacks", "Callbacks.cs");
        var callbackActions = await CodeFixTestSupport.RegisterActionsAsync(callbackDocument, callbackDiagnostic);
        var callbackAction = callbackActions.Single(action => action.Title == "Order Unity methods");
        var changed = await CodeFixTestSupport.ApplyActionAsync(callbackAction);

        var overrideDocument = CodeFixTestSupport.GetDocument(changed, "Callbacks", "Overrides.cs");
        var changedDiagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(changed.Projects.Single());
        var changedOverrideDiagnostic = Find(changedDiagnostics, "DGUA002", "Overrides.cs", "OnStateEnter");
        var overrideActions = await CodeFixTestSupport.RegisterActionsAsync(
            overrideDocument,
            changedOverrideDiagnostic);
        changed = await CodeFixTestSupport.ApplyActionAsync(
            overrideActions.Single(action => action.Title == "Order Unity methods"));

        var callbackText = (await CodeFixTestSupport.GetDocument(changed, "Callbacks", "Callbacks.cs").GetTextAsync()).ToString();
        var overrideText = (await CodeFixTestSupport.GetDocument(changed, "Callbacks", "Overrides.cs").GetTextAsync()).ToString();
        CodeFixTestSupport.AssertOrder(callbackText, "OnBeforeSerialize", "Awake", "Update", "OnAfterDeserialize", "Helper");
        CodeFixTestSupport.AssertOrder(overrideText, "OnStateEnter", "Helper");
        CodeFixTestSupport.AssertAnalyzerClear(
            await CodeFixTestSupport.GetDiagnosticsAsync(changed.Projects.Single()),
            "DGUA002");
        await CodeFixTestSupport.AssertCompilesAsync(changed.Projects.Single());
    }

    [Fact]
    public async Task CallbackFix_RespectsConfiguration()
    {
        var source = Source("ConfiguredCallbacks.cs", @"
public sealed class Example : MonoBehaviour
{
    public void Helper() { }
    public void Awake() { }
    public void Update() { }
    public void OnEnable() { }
}
");
        var solution = CodeFixTestSupport.CreateSolution(
            new TestProject(
                "Configured",
                new[] { source },
                new[]
                {
                    new TestAdditionalFile(
                        "Settings.DraasGames.Unity.Analyzers.additionalfile",
                        "draas_unity_callback_order = Awake,Update\n" +
                        "draas_unity_callbacks_before_other_methods = true\n")
                },
                new[]
                {
                    new TestAdditionalFile(
                        ".editorconfig",
                        "root = true\n\n[*.cs]\n" +
                        "draas_unity_callback_order = Update,Awake\n" +
                        "draas_unity_callbacks_before_other_methods = false\n")
                }));
        var project = solution.Projects.Single();
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(project);
        var diagnostic = Find(diagnostics, "DGUA002", "ConfiguredCallbacks.cs", "Update");
        var callbackDiagnostics = diagnostics.Where(item => item.Id == "DGUA002").ToArray();
        Assert.Single(callbackDiagnostics);
        Assert.Equal(
            "Update",
            callbackDiagnostics[0].Location.SourceTree!.GetText().ToString(callbackDiagnostics[0].Location.SourceSpan));

        var document = CodeFixTestSupport.GetDocument(solution, "Configured", "ConfiguredCallbacks.cs");
        var actions = await CodeFixTestSupport.RegisterActionsAsync(document, diagnostic);
        var action = actions.Single(item => item.Title == "Order Unity methods");
        var changed = await CodeFixTestSupport.ApplyActionAsync(action);
        var text = (await CodeFixTestSupport.GetDocument(changed, "Configured", "ConfiguredCallbacks.cs").GetTextAsync()).ToString();

        CodeFixTestSupport.AssertOrder(text, "Helper", "Update", "Awake", "OnEnable");
        CodeFixTestSupport.AssertAnalyzerClear(
            await CodeFixTestSupport.GetDiagnosticsAsync(changed.Projects.Single()),
            "DGUA002");
    }

    [Fact]
    public async Task CallbackFix_PreservesMemberContent()
    {
        const string crlf = "\r\n";
        var fieldText = "using System;" + crlf +
            "using UnityEngine;" + crlf + crlf +
            "public sealed class Fields" + crlf +
            "{" + crlf +
            "    public int ordinary;" + crlf +
            "    /// <summary>Keep this documentation.</summary>" + crlf +
            "    [Obsolete(\"keep\")]" + crlf +
            "    [SerializeField] // keep field trivia" + crlf +
            "    public int marked;" + crlf +
            "    public static int staticSlot;" + crlf +
            "}" + crlf;
        var methodText = "using System;" + crlf +
            "using UnityEngine;" + crlf + crlf +
            "public sealed class Methods : MonoBehaviour" + crlf +
            "{" + crlf +
            "    public int Property { get; } = 8;" + crlf +
            "    // helper comment" + crlf +
            "    public void Helper() { var marker = 1; }" + crlf +
            "    [Obsolete(\"callback\")]" + crlf +
            "    public void Update() { var body = 2; }" + crlf +
            "    public void Awake() { var body = 3; }" + crlf +
            "}" + crlf;
        var solution = CodeFixTestSupport.CreateSolution(
            new TestProject(
                "Content",
                new[] { new TestSource("Fields.cs", fieldText), new TestSource("Methods.cs", methodText) }));
        var fieldMembersBefore = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ordinary"] = MemberText(fieldText, "ordinary"),
            ["marked"] = MemberText(fieldText, "marked"),
            ["staticSlot"] = MemberText(fieldText, "staticSlot")
        };
        var methodMembersBefore = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Helper"] = MemberText(methodText, "Helper"),
            ["Update"] = MemberText(methodText, "Update"),
            ["Awake"] = MemberText(methodText, "Awake")
        };
        var project = solution.Projects.Single();
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(project);
        var fieldDiagnostic = Find(diagnostics, "DGUA001", "Fields.cs", "marked");
        var methodDiagnostic = Find(diagnostics, "DGUA002", "Methods.cs", "Awake");
        var fieldDocument = CodeFixTestSupport.GetDocument(solution, "Content", "Fields.cs");
        var methodDocument = CodeFixTestSupport.GetDocument(solution, "Content", "Methods.cs");

        var fieldActions = await CodeFixTestSupport.RegisterActionsAsync(fieldDocument, fieldDiagnostic);
        var changed = await CodeFixTestSupport.ApplyActionAsync(
            fieldActions.Single(action => action.Title == "Group serialized fields"));
        var methodActions = await CodeFixTestSupport.RegisterActionsAsync(
            CodeFixTestSupport.GetDocument(changed, "Content", "Methods.cs"),
            methodDiagnostic);
        changed = await CodeFixTestSupport.ApplyActionAsync(
            methodActions.Single(action => action.Title == "Order Unity methods"));

        var changedFieldText = (await CodeFixTestSupport.GetDocument(changed, "Content", "Fields.cs").GetTextAsync()).ToString();
        var changedMethodText = (await CodeFixTestSupport.GetDocument(changed, "Content", "Methods.cs").GetTextAsync()).ToString();
        Assert.Contains("\r\n", changedFieldText);
        Assert.Contains("\r\n", changedMethodText);
        Assert.Contains("<summary>Keep this documentation.</summary>", changedFieldText);
        Assert.Contains("[Obsolete(\"keep\")]", changedFieldText);
        Assert.Contains("keep field trivia", changedFieldText);
        Assert.Contains("// helper comment", changedMethodText);
        Assert.Contains("var body = 2", changedMethodText);
        Assert.Contains("var body = 3", changedMethodText);
        foreach (var member in fieldMembersBefore)
        {
            Assert.Equal(member.Value, MemberText(changedFieldText, member.Key));
        }

        foreach (var member in methodMembersBefore)
        {
            Assert.Equal(member.Value, MemberText(changedMethodText, member.Key));
        }

        CodeFixTestSupport.AssertOrder(changedFieldText, "marked", "ordinary", "staticSlot");
        CodeFixTestSupport.AssertOrder(changedMethodText, "Property", "Awake", "Update", "Helper");
        await CodeFixTestSupport.AssertCompilesAsync(changed.Projects.Single());
    }

    [Fact]
    public async Task FieldFix_GroupsAllSerializedMarkers()
    {
        var source = Source("Markers.cs", @"
using SF = UnityEngine.SerializeField;
using SR = UnityEngine.SerializeReference;
using OS = Sirenix.Serialization.OdinSerializeAttribute;

namespace Foreign
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class SerializeFieldAttribute : System.Attribute { }
}

public sealed class MarkerExample
{
    public int ordinaryBefore;
    [Foreign.SerializeField] public int foreignMarker;
    [SF] public int unityMarker;
    public int multiOne, multiTwo;
    public static int staticSlot;
    public const int constSlot = 3;
    [OS] public int odinMarker;
    public int ordinaryAfter;
    [SR] public int referenceMarker;
}
");
        var solution = CodeFixTestSupport.CreateSolution(new TestProject("Markers", new[] { source }));
        var project = solution.Projects.Single();
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(project);
        var diagnostic = Find(diagnostics, "DGUA001", "Markers.cs", "referenceMarker");
        var document = CodeFixTestSupport.GetDocument(solution, "Markers", "Markers.cs");
        var actions = await CodeFixTestSupport.RegisterActionsAsync(document, diagnostic);
        var changed = await CodeFixTestSupport.ApplyActionAsync(
            actions.Single(action => action.Title == "Group serialized fields"));
        var text = (await CodeFixTestSupport.GetDocument(changed, "Markers", "Markers.cs").GetTextAsync()).ToString();

        CodeFixTestSupport.AssertOrder(text, "unityMarker", "odinMarker", "referenceMarker", "foreignMarker", "ordinaryAfter");
        Assert.Contains("public int multiOne, multiTwo;", text);
        Assert.Contains("public static int staticSlot;", text);
        Assert.Contains("public const int constSlot = 3;", text);
        CodeFixTestSupport.AssertAnalyzerClear(await CodeFixTestSupport.GetDiagnosticsAsync(changed.Projects.Single()), "DGUA001");
        await CodeFixTestSupport.AssertCompilesAsync(changed.Projects.Single());
    }

    [Fact]
    public async Task FieldFix_RespectsConfiguration()
    {
        var source = Source("ConfiguredFields.cs", @"
public sealed class ConfiguredFields
{
    [SerializeField] public int marked;
    public int ordinary;
}
");
        var solution = CodeFixTestSupport.CreateSolution(
            new TestProject(
                "ConfiguredFields",
                new[] { source },
                new[]
                {
                    new TestAdditionalFile(
                        "Settings.DraasGames.Unity.Analyzers.additionalfile",
                        "draas_unity_serialized_field_placement = first\n")
                },
                new[]
                {
                    new TestAdditionalFile(
                        ".editorconfig",
                        "root = true\n\n[*.cs]\ndraas_unity_serialized_field_placement = last\n")
                }));
        var project = solution.Projects.Single();
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(project);
        var diagnostic = Find(diagnostics, "DGUA001", "ConfiguredFields.cs", "ordinary");
        var actions = await CodeFixTestSupport.RegisterActionsAsync(
            CodeFixTestSupport.GetDocument(solution, "ConfiguredFields", "ConfiguredFields.cs"),
            diagnostic);
        var changed = await CodeFixTestSupport.ApplyActionAsync(
            actions.Single(action => action.Title == "Group serialized fields"));
        var text = (await CodeFixTestSupport.GetDocument(changed, "ConfiguredFields", "ConfiguredFields.cs").GetTextAsync()).ToString();

        CodeFixTestSupport.AssertOrder(text, "ordinary", "marked");
        CodeFixTestSupport.AssertAnalyzerClear(await CodeFixTestSupport.GetDiagnosticsAsync(changed.Projects.Single()), "DGUA001");
    }

    [Fact]
    public async Task FieldFix_PreservesInitializerOrder()
    {
        var safe = Source("SafeFields.cs", @"
public sealed class SafeFields
{
    public int ordinary;
    [SerializeField] public int marked = Next();

    private static int Next() => 1;
}
");
        var unsafeSource = Source("InitializerFields.cs", @"
using System;

public sealed class FieldInitializerCrossing
{
    public int ordinary = Next();
    [SerializeField] public int marked = Next();

    private static int Next() => 1;
}

public sealed class FieldAndPropertyInitializers
{
    public int ordinary;
    public int Property { get; } = Next();
    [SerializeField] public int marked = Next();

    private static int Next() => 1;
}

public sealed class FieldAndEventInitializers
{
    public int ordinary;
    public event Action Event = Handler;
    [SerializeField] public int marked = Next();

    private static int Next() => 1;
    private static void Handler() { }
}
");
        var solution = CodeFixTestSupport.CreateSolution(
            new TestProject("Initializers", new[] { safe, unsafeSource }));
        var project = solution.Projects.Single();
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(project);
        var safeDiagnostic = Find(diagnostics, "DGUA001", "SafeFields.cs", "marked");
        var unsafeDiagnostics = diagnostics
            .Where(item => item.Id == "DGUA001" &&
                item.Location.SourceTree?.FilePath.EndsWith("InitializerFields.cs", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.Equal(3, unsafeDiagnostics.Length);
        var unsafeDocument = CodeFixTestSupport.GetDocument(solution, "Initializers", "InitializerFields.cs");
        var unsafeBefore = (await unsafeDocument.GetTextAsync()).ToString();
        foreach (var unsafeDiagnostic in unsafeDiagnostics)
        {
            Assert.Empty(await CodeFixTestSupport.RegisterActionsAsync(unsafeDocument, unsafeDiagnostic));
        }

        var safeActions = await CodeFixTestSupport.RegisterActionsAsync(
            CodeFixTestSupport.GetDocument(solution, "Initializers", "SafeFields.cs"),
            safeDiagnostic);
        var changed = await CodeFixTestSupport.ApplyActionAsync(
            safeActions.Single(action => action.Title == "Group serialized fields"));
        var unsafeAfter = (await CodeFixTestSupport.GetDocument(changed, "Initializers", "InitializerFields.cs").GetTextAsync()).ToString();
        Assert.Equal(unsafeBefore, unsafeAfter);
        var remaining = await CodeFixTestSupport.GetDiagnosticsAsync(changed.Projects.Single());
        Assert.DoesNotContain(remaining, item => item.Id == "DGUA001" && item.Location.SourceTree?.FilePath.EndsWith("SafeFields.cs", StringComparison.Ordinal) == true);
        Assert.Equal(3, remaining.Count(item => item.Id == "DGUA001" && item.Location.SourceTree?.FilePath.EndsWith("InitializerFields.cs", StringComparison.Ordinal) == true));
    }

    [Fact]
    public async Task FieldFix_DeclinesLayoutSensitiveTypes()
    {
        var source = Source("Layouts.cs", @"
using System.Runtime.InteropServices;

public struct LayoutStruct
{
    public int ordinary;
    [SerializeField] public int marked;
}

[StructLayout(LayoutKind.Sequential)]
public sealed class SequentialLayout
{
    public int ordinary;
    [SerializeField] public int marked;
}

[StructLayout(LayoutKind.Explicit)]
public sealed class ExplicitLayout
{
    [FieldOffset(0)] public int ordinary;
    [FieldOffset(4)] [SerializeField] public int marked;
}

public sealed class EligibleClass
{
    public int ordinary;
    [SerializeField] public int marked;
}
");
        var solution = CodeFixTestSupport.CreateSolution(new TestProject("Layouts", new[] { source }));
        var project = solution.Projects.Single();
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(project);
        var structDiagnostic = Find(diagnostics, "DGUA001", "Layouts.cs", "marked", occurrence: 1);
        var sequentialDiagnostic = Find(diagnostics, "DGUA001", "Layouts.cs", "marked", occurrence: 2);
        var explicitDiagnostic = Find(diagnostics, "DGUA001", "Layouts.cs", "marked", occurrence: 3);
        var eligibleDiagnostic = Find(diagnostics, "DGUA001", "Layouts.cs", "marked", occurrence: 4);
        var document = CodeFixTestSupport.GetDocument(solution, "Layouts", "Layouts.cs");
        var original = (await document.GetTextAsync()).ToString();
        Assert.Empty(await CodeFixTestSupport.RegisterActionsAsync(document, structDiagnostic));
        Assert.Empty(await CodeFixTestSupport.RegisterActionsAsync(document, sequentialDiagnostic));
        Assert.Empty(await CodeFixTestSupport.RegisterActionsAsync(document, explicitDiagnostic));

        var changed = await CodeFixTestSupport.ApplyActionAsync(
            (await CodeFixTestSupport.RegisterActionsAsync(document, eligibleDiagnostic))
            .Single(action => action.Title == "Group serialized fields"));
        var text = (await CodeFixTestSupport.GetDocument(changed, "Layouts", "Layouts.cs").GetTextAsync()).ToString();
        var originalNormalized = original.Replace("\r\n", "\n");
        var changedNormalized = text.Replace("\r\n", "\n");
        Assert.Equal(
            TextSection(originalNormalized, "public struct LayoutStruct", "[StructLayout(LayoutKind.Sequential)]"),
            TextSection(changedNormalized, "public struct LayoutStruct", "[StructLayout(LayoutKind.Sequential)]"));
        Assert.Equal(
            TextSection(originalNormalized, "[StructLayout(LayoutKind.Sequential)]", "[StructLayout(LayoutKind.Explicit)]"),
            TextSection(changedNormalized, "[StructLayout(LayoutKind.Sequential)]", "[StructLayout(LayoutKind.Explicit)]"));
        Assert.Equal(
            TextSection(originalNormalized, "[StructLayout(LayoutKind.Explicit)]", "public sealed class EligibleClass"),
            TextSection(changedNormalized, "[StructLayout(LayoutKind.Explicit)]", "public sealed class EligibleClass"));
        Assert.NotEqual(original, text);
        var eligibleSection = TextSection(changedNormalized, "public sealed class EligibleClass", null);
        CodeFixTestSupport.AssertOrder(eligibleSection, "marked;", "ordinary;");
    }

    [Fact]
    public async Task CodeFix_DirectivesSuppressAction()
    {
        var source = Source("Directives.cs", @"
public sealed class IfGuarded
{
#if true
    public int ordinary;
    [SerializeField] public int marked;
#endif
}

public sealed class RegionGuarded
{
#region fields
    public int ordinary;
    [SerializeField] public int marked;
#endregion
}

public sealed class PragmaGuarded
{
#pragma warning restore DGUA001
    public int ordinary;
    [SerializeField] public int marked;
}

public sealed class NullableGuarded
{
#nullable enable
    public int ordinary;
    [SerializeField] public int marked;
#nullable disable
}

public sealed class IfCallbacks : MonoBehaviour
{
#if true
    public void Helper() { }
    public void Awake() { }
#endif
}

public sealed class RegionCallbacks : MonoBehaviour
{
#region methods
    public void Helper() { }
    public void Awake() { }
#endregion
}

public sealed class PragmaCallbacks : MonoBehaviour
{
#pragma warning restore DGUA002
    public void Helper() { }
    public void Awake() { }
}

public sealed class NullableCallbacks : MonoBehaviour
{
#nullable enable
    public void Helper() { }
    public void Awake() { }
#nullable disable
}

public sealed class CleanFields
{
    public int ordinary;
    [SerializeField] public int marked;
}

public sealed class CleanCallbacks : MonoBehaviour
{
    public void Helper() { }
    public void Awake() { }
}
");
        var solution = CodeFixTestSupport.CreateSolution(new TestProject("Directives", new[] { source }));
        var project = solution.Projects.Single();
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(project);
        var guarded = diagnostics.Where(diagnostic => diagnostic.Id == "DGUA001").ToArray();
        Assert.Equal(5, guarded.Length);
        var document = CodeFixTestSupport.GetDocument(solution, "Directives", "Directives.cs");
        foreach (var diagnostic in guarded.Where(item =>
                     item.Location.SourceSpan.Start < document.GetSyntaxRootAsync().Result!.ToFullString().IndexOf("public sealed class CleanFields", StringComparison.Ordinal)))
        {
            Assert.Empty(await CodeFixTestSupport.RegisterActionsAsync(document, diagnostic));
        }

        var guardedCallbackDiagnostics = diagnostics
            .Where(item => item.Id == "DGUA002")
            .OrderBy(item => item.Location.SourceSpan.Start)
            .ToArray();
        Assert.Equal(5, guardedCallbackDiagnostics.Length);
        var cleanCallbacksStart = source.Text.IndexOf(
            "public sealed class CleanCallbacks",
            StringComparison.Ordinal);
        Assert.True(cleanCallbacksStart >= 0);
        foreach (var guardedCallbackDiagnostic in guardedCallbackDiagnostics.Where(item =>
                     item.Location.SourceSpan.Start < cleanCallbacksStart))
        {
            Assert.Empty(await CodeFixTestSupport.RegisterActionsAsync(document, guardedCallbackDiagnostic));
        }

        var cleanDiagnostic = Find(diagnostics, "DGUA001", "Directives.cs", "marked", occurrence: 5);
        var changed = await CodeFixTestSupport.ApplyActionAsync(
            (await CodeFixTestSupport.RegisterActionsAsync(document, cleanDiagnostic))
            .Single(action => action.Title == "Group serialized fields"));
        var cleanCallbackDiagnostic = (await CodeFixTestSupport.GetDiagnosticsAsync(changed.Projects.Single()))
            .Single(item => item.Id == "DGUA002" && item.Location.SourceSpan.Start > cleanCallbacksStart);
        changed = await CodeFixTestSupport.ApplyActionAsync(
            (await CodeFixTestSupport.RegisterActionsAsync(
                CodeFixTestSupport.GetDocument(changed, "Directives", "Directives.cs"),
                cleanCallbackDiagnostic))
            .Single(action => action.Title == "Order Unity methods"));
        var finalDiagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(changed.Projects.Single());
        var cleanFieldsStart = source.Text.IndexOf("public sealed class CleanFields", StringComparison.Ordinal);
        Assert.True(cleanFieldsStart >= 0);
        Assert.DoesNotContain(
            finalDiagnostics,
            item => item.Id == "DGUA001" && item.Location.SourceSpan.Start > cleanFieldsStart);
        Assert.Equal(4, finalDiagnostics.Count(item => item.Id == "DGUA001"));
        Assert.DoesNotContain(
            finalDiagnostics,
            item => item.Id == "DGUA002" && item.Location.SourceSpan.Start > cleanCallbacksStart);
        Assert.Equal(4, finalDiagnostics.Count(item => item.Id == "DGUA002"));
    }

    [Fact]
    public async Task CodeFix_PartialAndNestedTypesStayLocal()
    {
        var first = Source("Container.First.cs", @"
public partial class Container
{
    public int outerOrdinary;
    [SerializeField] public int outerMarked;
}
");
        var second = Source("Container.Second.cs", @"
public partial class Container
{
    public sealed class Nested
    {
        public int nestedOrdinary;
        [SerializeField] public int nestedMarked;
    }
}
");
        var solution = CodeFixTestSupport.CreateSolution(
            new TestProject("Local", new[] { first, second }));
        var project = solution.Projects.Single();
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(project);
        var nestedDiagnostic = Find(diagnostics, "DGUA001", "Container.Second.cs", "nestedMarked");
        var firstBefore = (await CodeFixTestSupport.GetDocument(solution, "Local", "Container.First.cs").GetTextAsync()).ToString();
        var secondBefore = (await CodeFixTestSupport.GetDocument(solution, "Local", "Container.Second.cs").GetTextAsync()).ToString();
        var actions = await CodeFixTestSupport.RegisterActionsAsync(
            CodeFixTestSupport.GetDocument(solution, "Local", "Container.Second.cs"),
            nestedDiagnostic);
        var changed = await CodeFixTestSupport.ApplyActionAsync(
            actions.Single(action => action.Title == "Group serialized fields"));
        var firstAfter = (await CodeFixTestSupport.GetDocument(changed, "Local", "Container.First.cs").GetTextAsync()).ToString();
        var secondAfter = (await CodeFixTestSupport.GetDocument(changed, "Local", "Container.Second.cs").GetTextAsync()).ToString();

        Assert.Equal(firstBefore, firstAfter);
        Assert.Contains("nestedOrdinary", secondBefore);
        Assert.NotEqual(secondBefore, secondAfter);
        CodeFixTestSupport.AssertOrder(secondAfter, "nestedMarked", "nestedOrdinary");
        Assert.Contains("outerOrdinary", firstAfter);
        Assert.Contains("outerMarked", firstAfter);
    }

    [Fact]
    public async Task CodeFix_CancellationDoesNotReturnChanges()
    {
        var solution = CodeFixTestSupport.CreateSolution(
            new TestProject(
                "Cancellation",
                new[]
                {
                    Source("Cancellation.cs", @"
public sealed class CancellationExample : MonoBehaviour
{
    public int ordinary;
    [SerializeField] public int marked;
    public void Helper() { }
    public void Awake() { }
}
")
                }));
        var project = solution.Projects.Single();
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(project);
        var fieldDiagnostic = Find(diagnostics, "DGUA001", "Cancellation.cs", "marked");
        var callbackDiagnostic = Find(diagnostics, "DGUA002", "Cancellation.cs", "Awake");
        var document = CodeFixTestSupport.GetDocument(solution, "Cancellation", "Cancellation.cs");

        using var registrationCancellation = new CancellationTokenSource();
        registrationCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CodeFixTestSupport.RegisterActionsAsync(document, fieldDiagnostic, registrationCancellation.Token));

        var actions = await CodeFixTestSupport.RegisterActionsAsync(document, fieldDiagnostic);
        using var applicationCancellation = new CancellationTokenSource();
        applicationCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CodeFixTestSupport.ApplyActionAsync(actions.Single(action => action.Title == "Group serialized fields"), applicationCancellation.Token));

        var callbackActions = await CodeFixTestSupport.RegisterActionsAsync(document, callbackDiagnostic);
        var key = callbackActions.Single(action => action.Title == "Order Unity methods").EquivalenceKey;
        Assert.NotNull(key);
        using var fixAllCancellation = new CancellationTokenSource();
        fixAllCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CodeFixTestSupport.ApplyFixAllAsync(
                solution,
                document,
                "DGUA002",
                FixAllScope.Document,
                key!,
                fixAllCancellation.Token));
    }

    [Fact]
    public async Task FixAll_DocumentFixesEveryAffectedType()
    {
        var source = Source("FixAll.Document.cs", @"
public sealed class FirstFields
{
    public int firstOrdinary;
    [SerializeField] public int firstMarked;
}

public sealed class Outer
{
    public int outerOrdinary;
    [SerializeField] public int outerMarked;
    public sealed class Nested
    {
        public int nestedOrdinary;
        [SerializeField] public int nestedMarked;
    }
}

public sealed class SecondFields
{
    public int secondOrdinary;
    [SerializeField] public int secondMarked;
}
");
        var solution = CodeFixTestSupport.CreateSolution(new TestProject("DocumentFixAll", new[] { source }));
        var project = solution.Projects.Single();
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(project);
        var fieldDiagnostic = Find(diagnostics, "DGUA001", "FixAll.Document.cs", "firstMarked");
        var document = CodeFixTestSupport.GetDocument(solution, "DocumentFixAll", "FixAll.Document.cs");
        var fieldAction = (await CodeFixTestSupport.RegisterActionsAsync(document, fieldDiagnostic))
            .Single(action => action.Title == "Group serialized fields");
        var fieldChanged = await CodeFixTestSupport.ApplyFixAllAsync(
            solution,
            document,
            "DGUA001",
            FixAllScope.Document,
            fieldAction.EquivalenceKey!);
        var fieldText = (await CodeFixTestSupport.GetDocument(fieldChanged, "DocumentFixAll", "FixAll.Document.cs").GetTextAsync()).ToString();
        CodeFixTestSupport.AssertOrder(fieldText, "firstMarked", "firstOrdinary");
        CodeFixTestSupport.AssertOrder(fieldText, "outerMarked", "outerOrdinary");
        CodeFixTestSupport.AssertOrder(fieldText, "nestedMarked", "nestedOrdinary");
        CodeFixTestSupport.AssertOrder(fieldText, "secondMarked", "secondOrdinary");
        CodeFixTestSupport.AssertAnalyzerClear(await CodeFixTestSupport.GetDiagnosticsAsync(fieldChanged.Projects.Single()), "DGUA001");

        var callbackSource = Source("FixAll.Callbacks.cs", @"
public sealed class FirstCallbacks : MonoBehaviour
{
    public void Helper() { }
    public void Awake() { }
}

public sealed class SecondCallbacks : MonoBehaviour
{
    public void Helper() { }
    public void Update() { }
    public void Awake() { }
}
");
        var callbackSolution = CodeFixTestSupport.CreateSolution(new TestProject("DocumentCallbackFixAll", new[] { callbackSource }));
        var callbackProject = callbackSolution.Projects.Single();
        var callbackDiagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(callbackProject);
        var callbackDiagnostic = Find(callbackDiagnostics, "DGUA002", "FixAll.Callbacks.cs", "Awake");
        var callbackDocument = CodeFixTestSupport.GetDocument(callbackSolution, "DocumentCallbackFixAll", "FixAll.Callbacks.cs");
        var callbackAction = (await CodeFixTestSupport.RegisterActionsAsync(callbackDocument, callbackDiagnostic))
            .Single(action => action.Title == "Order Unity methods");
        var callbackChanged = await CodeFixTestSupport.ApplyFixAllAsync(
            callbackSolution,
            callbackDocument,
            "DGUA002",
            FixAllScope.Document,
            callbackAction.EquivalenceKey!);
        var callbackText = (await CodeFixTestSupport.GetDocument(callbackChanged, "DocumentCallbackFixAll", "FixAll.Callbacks.cs").GetTextAsync()).ToString();
        var normalizedCallbackText = callbackText.Replace("\r\n", "\n");
        Assert.Contains(
            "public sealed class FirstCallbacks : MonoBehaviour\n{\n    public void Awake() { }\n    public void Helper() { }",
            normalizedCallbackText);
        Assert.Contains(
            "public sealed class SecondCallbacks : MonoBehaviour\n{\n    public void Awake() { }\n    public void Update() { }\n    public void Helper() { }",
            normalizedCallbackText);
        CodeFixTestSupport.AssertAnalyzerClear(await CodeFixTestSupport.GetDiagnosticsAsync(callbackChanged.Projects.Single()), "DGUA002");
    }

    [Fact]
    public async Task FixAll_ProjectRespectsScope()
    {
        var projectA = new TestProject(
            "SelectedProject",
            new[]
            {
                Source("A.First.cs", @"
public sealed class First
{
    public int ordinary;
    [SerializeField] public int marked;
}
"),
                Source("A.Second.cs", @"
public sealed class Second
{
    public int ordinary;
    [SerializeField] public int marked;
}
"),
                Source("A.Suppressed.cs", @"
public sealed class Suppressed
{
    public int ordinary;
    [SerializeField] public int marked;
}
")
            },
            editorConfigs: new[]
            {
                new TestAdditionalFile(
                    ".editorconfig",
                    "root = true\n\n[A.Suppressed.cs]\n" +
                    "dotnet_diagnostic.DGUA001.severity = none\n")
            });
        var projectB = new TestProject(
            "OtherProject",
            new[]
            {
                Source("B.Other.cs", @"
public sealed class Other
{
    public int ordinary;
    [SerializeField] public int marked;
}
")
            });
        var solution = CodeFixTestSupport.CreateSolution(projectA, projectB);
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(solution);
        var triggerDiagnostic = Find(diagnostics, "DGUA001", "A.First.cs", "marked");
        var triggerDocument = CodeFixTestSupport.GetDocument(solution, "SelectedProject", "A.First.cs");
        var action = (await CodeFixTestSupport.RegisterActionsAsync(triggerDocument, triggerDiagnostic))
            .Single(item => item.Title == "Group serialized fields");
        var changed = await CodeFixTestSupport.ApplyFixAllAsync(
            solution,
            triggerDocument,
            "DGUA001",
            FixAllScope.Project,
            action.EquivalenceKey!);
        var afterDiagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(changed);
        Assert.DoesNotContain(
            afterDiagnostics,
            diagnostic => diagnostic.Id == "DGUA001" &&
                diagnostic.Location.SourceTree?.FilePath.EndsWith("A.First.cs", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(
            afterDiagnostics,
            diagnostic => diagnostic.Id == "DGUA001" &&
                diagnostic.Location.SourceTree?.FilePath.EndsWith("A.Second.cs", StringComparison.Ordinal) == true);
        Assert.Contains(
            afterDiagnostics,
            diagnostic => diagnostic.Id == "DGUA001" &&
                diagnostic.Location.SourceTree?.FilePath.EndsWith("B.Other.cs", StringComparison.Ordinal) == true);
        var firstText = (await CodeFixTestSupport.GetDocument(changed, "SelectedProject", "A.First.cs").GetTextAsync()).ToString();
        var secondText = (await CodeFixTestSupport.GetDocument(changed, "SelectedProject", "A.Second.cs").GetTextAsync()).ToString();
        var suppressedText = (await CodeFixTestSupport.GetDocument(changed, "SelectedProject", "A.Suppressed.cs").GetTextAsync()).ToString();
        var otherText = (await CodeFixTestSupport.GetDocument(changed, "OtherProject", "B.Other.cs").GetTextAsync()).ToString();
        CodeFixTestSupport.AssertOrder(firstText, "marked", "ordinary");
        CodeFixTestSupport.AssertOrder(secondText, "marked", "ordinary");
        Assert.Contains("ordinary;\n    [SerializeField] public int marked;", suppressedText.Replace("\r\n", "\n"));
        Assert.Contains("ordinary;\n    [SerializeField] public int marked;", otherText.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task FixAll_SolutionUsesEachDocumentsSettings()
    {
        var projectA = new TestProject(
            "SettingsFirst",
            new[]
            {
                Source("A.Settings.cs", @"
public sealed class A : MonoBehaviour
{
    [SerializeField] public int marked;
    public int ordinary;
    public void Helper() { }
    public void Awake() { }
}
"),
                Source("A.Override.cs", @"
public sealed class AOverride : MonoBehaviour
{
    public int ordinary;
    [SerializeField] public int marked;
    public void Helper() { }
    public void Awake() { }
}
")
            },
            new[]
            {
                new TestAdditionalFile(
                    "A.DraasGames.Unity.Analyzers.additionalfile",
                    "draas_unity_serialized_field_placement = last\n")
            },
            editorConfigs: new[]
            {
                new TestAdditionalFile(
                    ".editorconfig",
                    "root = true\n\n[A.Override.cs]\n" +
                    "draas_unity_serialized_field_placement = first\n")
            });
        var projectB = new TestProject(
            "SettingsSecond",
            new[]
            {
                Source("B.Settings.cs", @"
public sealed class B : MonoBehaviour
{
    public int ordinary;
    [SerializeField] public int marked;
    public void Helper() { }
    public void Awake() { }
}
")
            },
            new[]
            {
                new TestAdditionalFile(
                    "B.DraasGames.Unity.Analyzers.additionalfile",
                    "draas_unity_serialized_field_placement = first\n")
            });
        var solution = CodeFixTestSupport.CreateSolution(projectA, projectB);
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(solution);
        var diagnostic = Find(diagnostics, "DGUA001", "A.Settings.cs", "ordinary");
        var trigger = CodeFixTestSupport.GetDocument(solution, "SettingsFirst", "A.Settings.cs");
        var action = (await CodeFixTestSupport.RegisterActionsAsync(trigger, diagnostic))
            .Single(item => item.Title == "Group serialized fields");
        var changed = await CodeFixTestSupport.ApplyFixAllAsync(
            solution,
            trigger,
            "DGUA001",
            FixAllScope.Solution,
            action.EquivalenceKey!);
        var aText = (await CodeFixTestSupport.GetDocument(changed, "SettingsFirst", "A.Settings.cs").GetTextAsync()).ToString();
        var overrideText = (await CodeFixTestSupport.GetDocument(changed, "SettingsFirst", "A.Override.cs").GetTextAsync()).ToString();
        var bText = (await CodeFixTestSupport.GetDocument(changed, "SettingsSecond", "B.Settings.cs").GetTextAsync()).ToString();
        CodeFixTestSupport.AssertOrder(aText, "ordinary", "marked");
        CodeFixTestSupport.AssertOrder(overrideText, "marked", "ordinary");
        CodeFixTestSupport.AssertOrder(bText, "marked", "ordinary");
        Assert.Contains("Helper", aText);
        Assert.Contains("Awake", aText);
        Assert.Contains("Helper", overrideText);
        Assert.Contains("Awake", overrideText);
        Assert.Contains("Helper", bText);
        Assert.Contains("Awake", bText);
        var afterDiagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(changed);
        CodeFixTestSupport.AssertAnalyzerClear(afterDiagnostics, "DGUA001");
        Assert.Equal(
            3,
            afterDiagnostics.Count(item => item.Id == "DGUA002"));
    }

    [Fact]
    public async Task FixAll_SkipsUnsafeTypesAndIsIdempotent()
    {
        var unsafeText = @"
public sealed class Unsafe
{
#region protected
    public int ordinary;
    [SerializeField] public int marked;
#endregion
}
";
        var source = Source("UnsafeAndSafe.cs", @"
public sealed class Safe
{
    public int ordinary;
    [SerializeField] public int marked;
}
" + unsafeText);
        var solution = CodeFixTestSupport.CreateSolution(new TestProject("Safety", new[] { source }));
        var project = solution.Projects.Single();
        var diagnostics = await CodeFixTestSupport.GetDiagnosticsAsync(project);
        var safeDiagnostic = Find(diagnostics, "DGUA001", "UnsafeAndSafe.cs", "marked", occurrence: 1);
        var unsafeDiagnostic = Find(diagnostics, "DGUA001", "UnsafeAndSafe.cs", "marked", occurrence: 2);
        var document = CodeFixTestSupport.GetDocument(solution, "Safety", "UnsafeAndSafe.cs");
        var before = (await document.GetTextAsync()).ToString();
        var unsafeStart = before.IndexOf("public sealed class Unsafe", StringComparison.Ordinal);
        var unsafeBefore = before.Substring(unsafeStart);
        Assert.Empty(await CodeFixTestSupport.RegisterActionsAsync(document, unsafeDiagnostic));
        var action = (await CodeFixTestSupport.RegisterActionsAsync(document, safeDiagnostic))
            .Single(item => item.Title == "Group serialized fields");
        var once = await CodeFixTestSupport.ApplyFixAllAsync(
            solution,
            document,
            "DGUA001",
            FixAllScope.Document,
            action.EquivalenceKey!);
        var afterOnce = (await CodeFixTestSupport.GetDocument(once, "Safety", "UnsafeAndSafe.cs").GetTextAsync()).ToString();
        var afterUnsafeStart = afterOnce.IndexOf("public sealed class Unsafe", StringComparison.Ordinal);
        Assert.Equal(unsafeBefore, afterOnce.Substring(afterUnsafeStart));
        CodeFixTestSupport.AssertOrder(afterOnce, "public sealed class Safe", "marked", "ordinary");

        var remaining = await CodeFixTestSupport.GetDiagnosticsAsync(once.Projects.Single());
        var remainingUnsafe = Find(remaining, "DGUA001", "UnsafeAndSafe.cs", "marked");
        Assert.Empty(await CodeFixTestSupport.RegisterActionsAsync(
            CodeFixTestSupport.GetDocument(once, "Safety", "UnsafeAndSafe.cs"),
            remainingUnsafe));
        var noOp = await CodeFixTestSupport.GetFixAllActionAsync(
            once,
            CodeFixTestSupport.GetDocument(once, "Safety", "UnsafeAndSafe.cs"),
            "DGUA001",
            FixAllScope.Document,
            action.EquivalenceKey!);
        Assert.Null(noOp);
    }

    private static TestSource Source(string filePath, string body)
    {
        return new TestSource(
            filePath,
            "using System;\n" +
            "using System.Collections;\n" +
            "using UnityEngine;\n" +
            "using Sirenix.Serialization;\n" +
            body);
    }

    private static Diagnostic Find(
        IEnumerable<Diagnostic> diagnostics,
        string id,
        string fileName,
        string identifier,
        int occurrence = 1)
    {
        var matches = diagnostics
            .Where(diagnostic => diagnostic.Id == id
                && diagnostic.Location.SourceTree?.FilePath.EndsWith(fileName, StringComparison.Ordinal) == true
                && string.Equals(
                    diagnostic.Location.SourceTree.GetText().ToString(diagnostic.Location.SourceSpan),
                    identifier,
                    StringComparison.Ordinal))
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ToArray();
        Assert.True(matches.Length >= occurrence, $"Expected {id} for {identifier} in {fileName}.");
        return matches[occurrence - 1];
    }

    private static string TextSection(string text, string startMarker, string? endMarker)
    {
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected section marker: " + startMarker);
        var end = endMarker == null
            ? text.Length
            : text.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end >= 0, "Expected section end marker: " + endMarker);
        return text.Substring(start, end - start);
    }

    private static string MemberText(string source, string identifier)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var member = root.DescendantNodes()
            .OfType<MemberDeclarationSyntax>()
            .FirstOrDefault(candidate => candidate switch
            {
                MethodDeclarationSyntax method => method.Identifier.ValueText == identifier,
                FieldDeclarationSyntax field => field.Declaration.Variables.Any(
                    variable => variable.Identifier.ValueText == identifier),
                _ => false
            });
        Assert.NotNull(member);
        return member!.ToFullString();
    }
}
