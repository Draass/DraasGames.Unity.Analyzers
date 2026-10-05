using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace DraasGames.Unity.Analyzers.Tests
{
public sealed class OrderingAnalyzerTests
{
    private const string UnityStubs = @"
namespace UnityEngine
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class SerializeField : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class SerializeReference : System.Attribute { }

    public class Object { }
    public class MonoBehaviour : Object { }
    public class ScriptableObject : Object { }
}

namespace Sirenix.Serialization
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class OdinSerializeAttribute : System.Attribute { }
}
";

    [Fact]
    public async Task SerializedFieldsFirst_NoDiagnostic()
    {
        var source = UnitySource(@"
public sealed class Example
{
    [SerializeField] public int unityField;
    [SerializeReference] public object referenceField = new object();
    [OdinSerialize] public int odinField;
    public int ordinaryField;
}
");

        var diagnostics = await AnalyzeAsync(source);

        AssertDiagnostics(diagnostics);
    }

    [Fact]
    public async Task SerializedFieldAfterOrdinaryField_Reports()
    {
        var source = UnitySource(@"
public sealed class Example
{
    public int ordinaryField;
    [SF] public int unityField;
    [SR] public int referenceField;
    [OS] public int odinField;
}
", "using SF = UnityEngine.SerializeField;\nusing SR = UnityEngine.SerializeReference;\nusing OS = Sirenix.Serialization.OdinSerializeAttribute;\n");

        var diagnostics = await AnalyzeAsync(source);

        AssertDiagnostics(
            diagnostics,
            Expect("DGUA001", source, "unityField"),
            Expect("DGUA001", source, "referenceField"),
            Expect("DGUA001", source, "odinField"));
    }

    [Fact]
    public async Task StaticAndConstFields_DoNotAffectOrdering()
    {
        var source = UnitySource(@"
public sealed class Example
{
    public static int staticBefore;
    public const int constantBefore = 1;
    [SerializeField] public int markedField;
    public static int staticMiddle;
    public const int constantMiddle = 2;
    public int ordinaryField;
    public static int staticAfter;
    public const int constantAfter = 3;
    [SerializeField] public int markedAfterOrdinary;
}
");

        var diagnostics = await AnalyzeAsync(source);

        AssertDiagnostics(diagnostics, Expect("DGUA001", source, "markedAfterOrdinary"));
    }

    [Fact]
    public async Task UnrelatedAttributes_AreIgnored()
    {
        var source = UnitySource(@"
namespace Other
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class SerializeFieldAttribute : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class SerializeReferenceAttribute : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class OdinSerializeAttribute : System.Attribute { }
}

public sealed class Example
{
    public int ordinaryField;
    [Other.SerializeField] public int fakeUnityField;
    [Other.SerializeReference] public int fakeReferenceField;
    [Other.OdinSerialize] public int fakeOdinField;
}
");

        var diagnostics = await AnalyzeAsync(source);

        AssertDiagnostics(diagnostics);
    }

    [Fact]
    public async Task SerializedFieldsLast_UsesConfiguration()
    {
        var source = UnitySource(@"
public sealed class Example
{
    [SerializeField] public int markedField;
    public int ordinaryField;
}
");
        var settings = Settings(
            "Settings.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_serialized_field_placement = last\n");

        var diagnostics = await AnalyzeAsync(source, settings);

        AssertDiagnostics(diagnostics, Expect("DGUA001", source, "ordinaryField"));
    }

    [Fact]
    public async Task Ordering_IsLocalToTypeDeclaration()
    {
        var firstPart = UnitySource(@"
public partial class Container : MonoBehaviour
{
    public int ordinaryInFirstPart;
    public void Update() { }
}
", filePath: "Container.First.cs");
        var secondPart = UnitySource(@"
public partial class Container
{
    [SerializeField] public int markedInSecondPart;
    public void Awake() { }

    public sealed class Nested
        : MonoBehaviour
    {
        public int nestedOrdinaryField;
        [SerializeField] public int nestedMarkedField;
        public void Update() { }
        public void OnEnable() { }
    }
}
", filePath: "Container.Second.cs");

        var diagnostics = await AnalyzeAsync(firstPart, secondPart);

        AssertDiagnostics(
            diagnostics,
            Expect("DGUA001", secondPart, "nestedMarkedField"),
            Expect("DGUA002", secondPart, "OnEnable"));
    }

    [Fact]
    public async Task UnityCallbacksOutOfOrder_Report()
    {
        var source = UnitySource(@"
public class IndirectBehaviour : MonoBehaviour { }

public sealed class BehaviourExample : IndirectBehaviour
{
    public void Update() { }
    public void Awake() { }
}

public sealed class AssetExample : ScriptableObject
{
    public void OnDisable() { }
    public void OnValidate() { }
    public void Update() { }
}
");

        var diagnostics = await AnalyzeAsync(source);

        AssertDiagnostics(
            diagnostics,
            Expect("DGUA002", source, "Awake"),
            Expect("DGUA002", source, "OnValidate"));
    }

    [Fact]
    public async Task UnityCallbacksInConfiguredOrder_NoDiagnostic()
    {
        var defaultSource = UnitySource(@"
public sealed class DefaultOrder : MonoBehaviour
{
    public void Reset() { }
    public void Awake() { }
    public void Update() { }
}
");
        var customSource = UnitySource(@"
public sealed class CustomOrder : MonoBehaviour
{
    public void Update() { }
    public void Awake() { }
    public void Start() { }
    public void LateUpdate() { }
}
");
        var customSettings = Settings(
            "Custom.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_callback_order = Update,Awake,Start\n");

        var defaultDiagnostics = await AnalyzeAsync(defaultSource);
        var customDiagnostics = await AnalyzeAsync(customSource, customSettings);

        AssertDiagnostics(defaultDiagnostics);
        AssertDiagnostics(customDiagnostics);
    }

    [Fact]
    public async Task OrdinaryMethodsAndInvalidSignatures_AreIgnored()
    {
        var source = UnitySource(@"
public sealed class NonUnityExample
{
    public void Update() { }
    public void Awake() { }
}

public sealed class StaticCallbackBehaviour : MonoBehaviour
{
    public static void Update() { }
    public void Awake() { }
}

public sealed class CoroutineCallbackBehaviour : MonoBehaviour
{
    public void Update() { }
    public IEnumerator Awake() { yield break; }
}

public sealed class GenericCallbackBehaviour : MonoBehaviour
{
    public void Update<T>() { }
    public void Awake() { }
}

public sealed class ParameterCallbackBehaviour : MonoBehaviour
{
    public void Update(int value) { }
    public void Awake() { }
}

public sealed class ReturnTypeCallbackBehaviour : MonoBehaviour
{
    public void Update() { }
    public int Start() { return 0; }
}

public abstract class AbstractCallbackBehaviour : MonoBehaviour
{
    public void OnDestroy() { }
    public abstract void OnDisable();
}

public sealed class InvalidAsset : ScriptableObject
{
    public void Update() { }
    public void Awake() { }
}
");

        var diagnostics = await AnalyzeAsync(source);

        AssertDiagnostics(diagnostics);
    }

    [Fact]
    public async Task CoroutineStart_IsRecognized()
    {
        var source = UnitySource(@"
public sealed class CoroutineBehaviour : MonoBehaviour
{
    public void Update() { }
    public IEnumerator Start() { yield break; }
}
");

        var diagnostics = await AnalyzeAsync(source);

        AssertDiagnostics(diagnostics, Expect("DGUA002", source, "Start"));
    }

    [Fact]
    public async Task GeneratedCode_IsIgnored()
    {
        var generatedHeaderSource = GeneratedUnitySource(@"
public sealed class HeaderGenerated : MonoBehaviour
{
    public int ordinaryField;
    [SerializeField] public int markedField;
    public void Update() { }
    public void Awake() { }
}
");
        var generatedFileSource = UnitySource(@"
public sealed class FileGenerated : MonoBehaviour
{
    public int ordinaryField;
    [SerializeField] public int markedField;
    public void Update() { }
    public void Awake() { }
}
", filePath: "Generated.g.cs");

        var headerDiagnostics = await AnalyzeAsync(generatedHeaderSource);
        var fileDiagnostics = await AnalyzeAsync(generatedFileSource);

        AssertDiagnostics(headerDiagnostics);
        AssertDiagnostics(fileDiagnostics);
    }

    [Fact]
    public async Task EditorConfig_OverridesAdditionalFile()
    {
        var source = UnitySource(@"
public sealed class Example : MonoBehaviour
{
    [SerializeField] public int markedField;
    public int ordinaryField;
    public void Update() { }
    public void Awake() { }
}
");
        var settings = Settings(
            "Settings.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_serialized_field_placement = first\n" +
            "draas_unity_callback_order = Reset,OnValidate,Awake,OnEnable,Start,FixedUpdate,Update,LateUpdate,OnDisable,OnDestroy\n");
        var treeOptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["draas_unity_serialized_field_placement"] = "last",
            ["draas_unity_callback_order"] = "Update,Awake"
        };

        var diagnostics = await AnalyzeAsync(source, settings, treeOptions);

        AssertDiagnostics(diagnostics, Expect("DGUA001", source, "ordinaryField"));
    }

    [Fact]
    public async Task InvalidOptions_UseDefaults()
    {
        var placementSource = UnitySource(@"
public sealed class PlacementExample
{
    public int ordinaryField;
    [SerializeField] public int markedField;
}
");
        var duplicateCallbackSource = UnitySource(@"
public sealed class DuplicateCallbackExample : MonoBehaviour
{
    public void Update() { }
    public void Awake() { }
}
");
        var emptyCallbackSource = UnitySource(@"
public sealed class EmptyCallbackExample : MonoBehaviour
{
    public void Update() { }
    public void Awake() { }
}
");

        var invalidPlacement = Settings(
            "InvalidPlacement.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_serialized_field_placement = middle\n");
        var duplicateCallbacks = Settings(
            "DuplicateCallbacks.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_callback_order = Update,Awake,Update\n");
        var unknownCallbacks = Settings(
            "UnknownCallbacks.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_callback_order = Update,Awake,NotARealCallback\n");
        var emptyCallbacks = Settings(
            "EmptyCallbacks.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_callback_order = \n");

        var placementDiagnostics = await AnalyzeAsync(placementSource, invalidPlacement);
        var duplicateDiagnostics = await AnalyzeAsync(duplicateCallbackSource, duplicateCallbacks);
        var unknownDiagnostics = await AnalyzeAsync(duplicateCallbackSource, unknownCallbacks);
        var emptyDiagnostics = await AnalyzeAsync(emptyCallbackSource, emptyCallbacks);

        AssertDiagnostics(placementDiagnostics, Expect("DGUA001", placementSource, "markedField"));
        AssertDiagnostics(duplicateDiagnostics, Expect("DGUA002", duplicateCallbackSource, "Awake"));
        AssertDiagnostics(unknownDiagnostics, Expect("DGUA002", duplicateCallbackSource, "Awake"));
        AssertDiagnostics(emptyDiagnostics, Expect("DGUA002", emptyCallbackSource, "Awake"));
    }

    [Fact]
    public async Task AdditionalFiles_AreFilteredAndMergedDeterministically()
    {
        var source = UnitySource(@"
public sealed class Example
{
    public int ordinaryField;
    [SerializeField] public int markedField;
}
");
        var laterByPath = Settings(
            "Z.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_serialized_field_placement = first\n");
        var earlierByPath = Settings(
            "A.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_serialized_field_placement = last\n");
        var foreign = Settings(
            "ZZForeign.DraasGames.Unity.Analyzers.additionalfile.txt",
            "draas_unity_serialized_field_placement = last\n");

        var diagnosticsInReverseOrder = await AnalyzeAsync(source, laterByPath, foreign, earlierByPath);
        var diagnosticsInForwardOrder = await AnalyzeAsync(source, earlierByPath, foreign, laterByPath);

        AssertDiagnostics(
            diagnosticsInReverseOrder,
            Expect("DGUA001", source, "markedField"));
        AssertDiagnostics(
            diagnosticsInForwardOrder,
            Expect("DGUA001", source, "markedField"));
    }

    [Fact]
    public async Task DisabledRules_DoNotReport()
    {
        var fieldSource = UnitySource(@"
public sealed class FieldExample
{
    public int ordinaryField;
    [SerializeField] public int markedField;
}
");
        var callbackSource = UnitySource(@"
public sealed class CallbackExample : MonoBehaviour
{
    public void Update() { }
    public void Awake() { }
}
");
        var fieldOptions = ImmutableDictionary<string, ReportDiagnostic>.Empty
            .Add("DGUA001", ReportDiagnostic.Suppress);
        var callbackOptions = ImmutableDictionary<string, ReportDiagnostic>.Empty
            .Add("DGUA002", ReportDiagnostic.Suppress);

        var fieldDiagnostics = await AnalyzeAsync(fieldSource, fieldOptions);
        var callbackDiagnostics = await AnalyzeAsync(callbackSource, callbackOptions);

        AssertDiagnostics(fieldDiagnostics);
        AssertDiagnostics(callbackDiagnostics);
    }

    private static SourceInput UnitySource(string body, string prefix = "", string filePath = "Test.cs")
    {
        return new SourceInput(
            filePath,
            prefix +
            "using System;\n" +
            "using System.Collections;\n" +
            "using UnityEngine;\n" +
            "using Sirenix.Serialization;\n" +
            body);
    }

    private static SourceInput GeneratedUnitySource(string body)
    {
        return new SourceInput(
            "GeneratedHeader.cs",
            "// <auto-generated />\n" +
            "using System;\n" +
            "using System.Collections;\n" +
            "using UnityEngine;\n" +
            "using Sirenix.Serialization;\n" +
            body);
    }

    private static AdditionalFile Settings(string path, string content)
    {
        return new AdditionalFile(path, content);
    }

    private static ExpectedDiagnostic Expect(string id, SourceInput source, string identifier)
    {
        return new ExpectedDiagnostic(id, source, identifier);
    }

    private static void AssertDiagnostics(
        ImmutableArray<Diagnostic> actual,
        params ExpectedDiagnostic[] expected)
    {
        Assert.All(actual, diagnostic => Assert.NotEqual("AD0001", diagnostic.Id));
        Assert.Equal(expected.Length, actual.Length);

        foreach (var expectedDiagnostic in expected)
        {
            var expectedStart = expectedDiagnostic.Source.Text.IndexOf(
                expectedDiagnostic.Identifier,
                StringComparison.Ordinal);
            Assert.True(
                expectedStart >= 0,
                "The expected identifier was not found in the test source: " + expectedDiagnostic.Identifier);

            var matches = actual
                .Where(diagnostic =>
                    diagnostic.Id == expectedDiagnostic.Id &&
                    diagnostic.Location.SourceTree != null &&
                    diagnostic.Location.SourceTree.FilePath == expectedDiagnostic.Source.FilePath &&
                    diagnostic.Location.SourceSpan.Start == expectedStart)
                .ToArray();

            Assert.Single(matches);
            var diagnostic = matches[0];
            Assert.Equal(
                expectedDiagnostic.Identifier,
                expectedDiagnostic.Source.Text.Substring(
                    diagnostic.Location.SourceSpan.Start,
                    diagnostic.Location.SourceSpan.Length));
        }
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        params SourceInput[] sources)
    {
        return await AnalyzeAsync(
            (IEnumerable<SourceInput>)sources,
            Array.Empty<AdditionalFile>(),
            null,
            null);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        SourceInput source,
        AdditionalFile additionalFile,
        Dictionary<string, string>? treeOptions = null)
    {
        return await AnalyzeAsync(
            new[] { source },
            new[] { additionalFile },
            treeOptions,
            null);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        SourceInput source,
        AdditionalFile first,
        AdditionalFile second,
        AdditionalFile third)
    {
        return await AnalyzeAsync(
            new[] { source },
            new[] { first, second, third },
            null,
            null);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        SourceInput source,
        IEnumerable<AdditionalFile> additionalFiles,
        Dictionary<string, string>? treeOptions = null,
        ImmutableDictionary<string, ReportDiagnostic>? specificDiagnosticOptions = null)
    {
        return await AnalyzeAsync(
            new[] { source },
            additionalFiles,
            treeOptions,
            specificDiagnosticOptions);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        SourceInput source,
        ImmutableDictionary<string, ReportDiagnostic> specificDiagnosticOptions)
    {
        return await AnalyzeAsync(
            new[] { source },
            Array.Empty<AdditionalFile>(),
            null,
            specificDiagnosticOptions);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        IEnumerable<SourceInput> sources,
        IEnumerable<AdditionalFile> additionalFiles,
        Dictionary<string, string>? treeOptions,
        ImmutableDictionary<string, ReportDiagnostic>? specificDiagnosticOptions)
    {
        var sourceList = sources.ToArray();
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp9);
        var syntaxTrees = sourceList
            .Select(source => CSharpSyntaxTree.ParseText(
                SourceText.From(source.Text, Encoding.UTF8),
                parseOptions,
                source.FilePath))
            .Prepend(CSharpSyntaxTree.ParseText(
                SourceText.From(UnityStubs, Encoding.UTF8),
                parseOptions,
                "UnityStubs.cs"))
            .ToImmutableArray();
        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            nullableContextOptions: NullableContextOptions.Disable,
            specificDiagnosticOptions: specificDiagnosticOptions ??
                ImmutableDictionary<string, ReportDiagnostic>.Empty);
        var compilation = CSharpCompilation.Create(
            "DraasGames.Unity.Analyzers.Tests",
            syntaxTrees,
            RuntimeReferences(),
            compilationOptions);

        var compilationErrors = compilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(compilationErrors);

        var additionalTexts = additionalFiles
            .Select(file => (AdditionalText)new InMemoryAdditionalText(file.FilePath, file.Text))
            .ToImmutableArray();
        var analyzerOptions = new AnalyzerOptions(
            additionalTexts,
            new TestAnalyzerConfigOptionsProvider(treeOptions));
        var withAnalyzersOptions = new CompilationWithAnalyzersOptions(
            analyzerOptions,
            onAnalyzerException: null!,
            concurrentAnalysis: false,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new SerializedFieldOrderAnalyzer(),
            new UnityCallbackOrderAnalyzer());

        return await compilation
            .WithAnalyzers(analyzers, withAnalyzersOptions)
            .GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<MetadataReference> RuntimeReferences()
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

    private sealed class SourceInput
    {
        public SourceInput(string filePath, string text)
        {
            FilePath = filePath;
            Text = text;
        }

        public string FilePath { get; }

        public string Text { get; }
    }

    private sealed class AdditionalFile
    {
        public AdditionalFile(string filePath, string text)
        {
            FilePath = filePath;
            Text = text;
        }

        public string FilePath { get; }

        public string Text { get; }
    }

    private sealed class ExpectedDiagnostic
    {
        public ExpectedDiagnostic(string id, SourceInput source, string identifier)
        {
            Id = id;
            Source = source;
            Identifier = identifier;
        }

        public string Id { get; }

        public SourceInput Source { get; }

        public string Identifier { get; }
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText text;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return text;
        }
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions options;

        public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string>? values)
        {
            options = new TestAnalyzerConfigOptions(values ??
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        public override AnalyzerConfigOptions GlobalOptions => options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return options;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return options;
        }
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly IReadOnlyDictionary<string, string> values;

        public TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values)
        {
            this.values = values;
        }

        public override bool TryGetValue(string key, out string value)
        {
            if (values.TryGetValue(key, out var configuredValue))
            {
                value = configuredValue;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}
}
