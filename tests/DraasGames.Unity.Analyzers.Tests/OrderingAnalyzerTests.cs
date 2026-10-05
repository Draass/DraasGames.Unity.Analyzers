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
    public class Component : Object { }
    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { }
    public class Animator : Behaviour { }
    public struct AnimatorStateInfo { }
    public class AnimationClip : Object { }
    public class AudioClip : Object { }
    public class AudioSource : Behaviour { }
    public class Camera : Behaviour { }
    public class Collider : Component { }
    public class Collider2D : Component { }
    public class Collision { }
    public class Collision2D { }
    public class ControllerColliderHit { }
    public class GameObject : Object { }
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

namespace UnityEngine.Networking
{
    public class NetworkBehaviour : UnityEngine.MonoBehaviour { }
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

        var placementDisabled = Settings(
            "PlacementDisabled.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_callbacks_before_other_methods = false\n");
        var diagnostics = await AnalyzeAsync(source, placementDisabled);

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
        var firstDirectionSource = UnitySource(@"
public sealed class FirstDirection : MonoBehaviour
{
    [SerializeField] public int markedField;
    public int ordinaryField;
    public void Helper() { }
    public void Update() { }
    public void Awake() { }
}
", filePath: "EditorConfig.First.cs");
        var firstDirectionSettings = Settings(
            "First.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_serialized_field_placement = first\n" +
            "draas_unity_callback_order = Update,Awake\n" +
            "draas_unity_callbacks_before_other_methods = false\n");
        var firstDirectionOptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["draas_unity_serialized_field_placement"] = "last",
            ["draas_unity_callback_order"] = "Update,Awake",
            ["draas_unity_callbacks_before_other_methods"] = "true"
        };

        var secondDirectionSource = UnitySource(@"
public sealed class SecondDirection : MonoBehaviour
{
    [SerializeField] public int markedField;
    public int ordinaryField;
    public void Helper() { }
    public void Update() { }
    public void Awake() { }
}
", filePath: "EditorConfig.Second.cs");
        var secondDirectionSettings = Settings(
            "Second.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_serialized_field_placement = last\n" +
            "draas_unity_callback_order = Update,Awake\n" +
            "draas_unity_callbacks_before_other_methods = true\n");
        var secondDirectionOptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["draas_unity_serialized_field_placement"] = "first",
            ["draas_unity_callback_order"] = "Update,Awake",
            ["draas_unity_callbacks_before_other_methods"] = "false"
        };

        var firstDirectionDiagnostics = await AnalyzeAsync(
            firstDirectionSource,
            firstDirectionSettings,
            firstDirectionOptions);
        var secondDirectionDiagnostics = await AnalyzeAsync(
            secondDirectionSource,
            secondDirectionSettings,
            secondDirectionOptions);

        AssertDiagnostics(
            firstDirectionDiagnostics,
            Expect("DGUA001", firstDirectionSource, "ordinaryField"),
            Expect("DGUA002", firstDirectionSource, "Update"),
            Expect("DGUA002", firstDirectionSource, "Awake"));
        AssertDiagnostics(secondDirectionDiagnostics);
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
        var invalidBooleanSource = UnitySource(@"
public sealed class InvalidBooleanExample : MonoBehaviour
{
    public void Helper() { }
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
        var invalidBoolean = Settings(
            "InvalidBoolean.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_callbacks_before_other_methods = maybe\n");
        var emptyBoolean = Settings(
            "EmptyBoolean.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_callbacks_before_other_methods = \n");

        var placementDiagnostics = await AnalyzeAsync(placementSource, invalidPlacement);
        var duplicateDiagnostics = await AnalyzeAsync(duplicateCallbackSource, duplicateCallbacks);
        var unknownDiagnostics = await AnalyzeAsync(duplicateCallbackSource, unknownCallbacks);
        var emptyDiagnostics = await AnalyzeAsync(emptyCallbackSource, emptyCallbacks);
        var invalidBooleanDiagnostics = await AnalyzeAsync(invalidBooleanSource, invalidBoolean);
        var emptyBooleanDiagnostics = await AnalyzeAsync(invalidBooleanSource, emptyBoolean);

        AssertDiagnostics(placementDiagnostics, Expect("DGUA001", placementSource, "markedField"));
        AssertDiagnostics(duplicateDiagnostics, Expect("DGUA002", duplicateCallbackSource, "Awake"));
        AssertDiagnostics(unknownDiagnostics, Expect("DGUA002", duplicateCallbackSource, "Awake"));
        AssertDiagnostics(emptyDiagnostics, Expect("DGUA002", emptyCallbackSource, "Awake"));
        AssertDiagnostics(
            invalidBooleanDiagnostics,
            Expect("DGUA002", invalidBooleanSource, "Awake"));
        AssertDiagnostics(
            emptyBooleanDiagnostics,
            Expect("DGUA002", invalidBooleanSource, "Awake"));
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

    [Fact]
    public async Task UnityCallbackAfterOrdinaryMethod_Reports()
    {
        var monoSource = UnitySource(@"
public sealed class PlacementBehaviour : MonoBehaviour
{
    public void Helper() { }
    public static void StaticHelper() { }
    public void GenericHelper<T>() { }
    public override string ToString() { return ""ordinary""; }
    public void Update() { }
    public void Awake() { }
    public void OnEnable() { }
}
", filePath: "Placement.Mono.cs");
        var assetSource = UnitySource(@"
public sealed class PlacementAsset : ScriptableObject
{
    public void Helper() { }
    public void Awake() { }
    public void OnDisable() { }
}
", filePath: "Placement.Asset.cs");

        var diagnostics = await AnalyzeAsync(monoSource, assetSource);

        AssertDiagnostics(
            diagnostics,
            Expect("DGUA002", monoSource, "Update"),
            Expect("DGUA002", monoSource, "Awake"),
            Expect("DGUA002", monoSource, "OnEnable"),
            Expect("DGUA002", assetSource, "Awake"),
            Expect("DGUA002", assetSource, "OnDisable"));
    }

    [Fact]
    public async Task UnityCallbacksBeforeOrdinaryMethods_NoDiagnostic()
    {
        var source = UnitySource(@"
public sealed class ValidPlacement : MonoBehaviour
{
    public int fieldBefore;
    public ValidPlacement() { }
    public int PropertyBefore { get; set; }
    public void Awake() { }
    public void Update() { }
    public int fieldAfter;
    public int PropertyAfter { get; set; }
    public void Helper() { }
}
");

        var diagnostics = await AnalyzeAsync(source);

        AssertDiagnostics(diagnostics);
    }

    [Fact]
    public async Task CallbackPlacementDisabled_PreservesRelativeOrder()
    {
        var source = UnitySource(@"
public sealed class DisabledPlacement : MonoBehaviour
{
    public void Helper() { }
    public void Update() { }
    public void Awake() { }
}
");
        var settings = Settings(
            "PlacementFalse.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_callbacks_before_other_methods = false\n");

        var diagnostics = await AnalyzeAsync(source, settings);

        AssertDiagnostics(diagnostics, Expect("DGUA002", source, "Awake"));
    }

    [Fact]
    public async Task UnrankedUnityCallbacks_StillPrecedeOrdinaryMethods()
    {
        var source = UnitySource(@"
public sealed class UnrankedCallbacks : MonoBehaviour
{
    public void Helper() { }
    public void Update() { }
    public void OnTriggerEnter(Collider value) { }
    public void Start() { }
    public void OnDestroy() { }
    public void Awake() { }
}
");
        var beforeHelperSource = UnitySource(@"
public sealed class UnrankedCallbacksBeforeHelper : MonoBehaviour
{
    public void Update() { }
    public void OnTriggerEnter(Collider value) { }
    public void Start() { }
    public void OnDestroy() { }
    public void Awake() { }
    public void Helper() { }
}
", filePath: "Unranked.BeforeHelper.cs");
        var settings = Settings(
            "Subset.DraasGames.Unity.Analyzers.additionalfile",
            "draas_unity_callback_order = Update,Awake\n");

        var diagnostics = await AnalyzeAsync(
            new[] { source, beforeHelperSource },
            new[] { settings },
            null,
            null);

        AssertDiagnostics(
            diagnostics,
            Expect("DGUA002", source, "Update"),
            Expect("DGUA002", source, "OnTriggerEnter"),
            Expect("DGUA002", source, "Start"),
            Expect("DGUA002", source, "OnDestroy"),
            Expect("DGUA002", source, "Awake"));
        AssertDiagnostics(
            diagnostics
                .Where(diagnostic =>
                    diagnostic.Location.SourceTree?.FilePath == beforeHelperSource.FilePath)
                .ToImmutableArray());
    }

    [Fact]
    public async Task ExtendedUnityMessages_RespectSignatures()
    {
        var physicsSource = UnitySource(@"
public sealed class PhysicsMessages : MonoBehaviour
{
    public void Helper() { }
    public void OnCollisionEnter(Collision value) { }
    public void OnCollisionEnter2D(Collision2D value) { }
    public void OnTriggerEnter(Collider value) { }
    public void OnTriggerEnter2D(Collider2D value) { }
}
", filePath: "Extended.Physics.cs");
        var applicationSource = UnitySource(@"
public sealed class ApplicationMessages : MonoBehaviour
{
    public void Helper() { }
    public void OnApplicationFocus(bool value) { }
    public void OnApplicationPause(bool value) { }
}
", filePath: "Extended.Application.cs");
        var audioRenderSource = UnitySource(@"
public sealed class AudioRenderMessages : MonoBehaviour
{
    public void Helper() { }
    public void OnAudioFilterRead(float[] data, int channels) { }
    public void OnRenderImage(RenderTexture source, RenderTexture destination) { }
}
", filePath: "Extended.AudioRender.cs");
        var transformParticleGuiSource = UnitySource(@"
public sealed class TransformParticleGuiMessages : MonoBehaviour
{
    public void Helper() { }
    public void OnTransformChildrenChanged() { }
    public void OnParticleCollision(GameObject value) { }
    public void OnGUI() { }
}
", filePath: "Extended.TransformParticleGui.cs");
        var scriptableObjectSource = UnitySource(@"
public sealed class ScriptableAsset : ScriptableObject
{
    public void Helper() { }
    public void Reset() { }
}
", filePath: "Extended.ScriptableObject.cs");
        var editorWindowSource = UnitySource(@"
public sealed class WindowMessages : UnityEditor.EditorWindow
{
    public void Helper() { }
    public void CreateGUI() { }
}
", filePath: "Extended.EditorWindow.cs");
        var assetPostprocessorSource = UnitySource(@"
public sealed class ImportMessages : UnityEditor.AssetPostprocessor
{
    public void Helper() { }

    public static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths) { }
}
", filePath: "Extended.AssetPostprocessor.cs");
        var wrongCounterpartsSource = UnitySource(@"
public sealed class WrongMessageSignatures : MonoBehaviour
{
    public void OnCollisionEnter(int value) { }
    public void OnApplicationFocus(int value) { }
    public void OnAudioFilterRead(float data, int channels) { }
    public void OnRenderImage(RenderTexture source) { }
    public void OnTransformChildrenChanged(int value) { }
    public void OnParticleCollision(int value) { }
    public void OnGUI(int value) { }
    public void Awake() { }
}
", filePath: "Extended.WrongSignatures.cs");

        var diagnostics = await AnalyzeAsync(
            physicsSource,
            applicationSource,
            audioRenderSource,
            transformParticleGuiSource,
            scriptableObjectSource,
            editorWindowSource,
            assetPostprocessorSource,
            wrongCounterpartsSource);

        AssertDiagnostics(
            diagnostics,
            Expect("DGUA002", physicsSource, "OnCollisionEnter"),
            Expect("DGUA002", physicsSource, "OnCollisionEnter2D"),
            Expect("DGUA002", physicsSource, "OnTriggerEnter"),
            Expect("DGUA002", physicsSource, "OnTriggerEnter2D"),
            Expect("DGUA002", applicationSource, "OnApplicationFocus"),
            Expect("DGUA002", applicationSource, "OnApplicationPause"),
            Expect("DGUA002", audioRenderSource, "OnAudioFilterRead"),
            Expect("DGUA002", audioRenderSource, "OnRenderImage"),
            Expect("DGUA002", transformParticleGuiSource, "OnTransformChildrenChanged"),
            Expect("DGUA002", transformParticleGuiSource, "OnParticleCollision"),
            Expect("DGUA002", transformParticleGuiSource, "OnGUI"),
            Expect("DGUA002", scriptableObjectSource, "Reset"),
            Expect("DGUA002", editorWindowSource, "CreateGUI"),
            Expect("DGUA002", assetPostprocessorSource, "OnPostprocessAllAssets"),
            Expect("DGUA002", wrongCounterpartsSource, "Awake"));
    }

    [Fact]
    public async Task UnityApiOverrides_AreGroupedBeforeOrdinaryMethods()
    {
        var stateBaseSource = UnitySource(@"
public abstract class IntermediateState : UnityEngine.StateMachineBehaviour
{
    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex) { }
}
", filePath: "Overrides.StateBase.cs");
        var stateSource = UnitySource(@"
public sealed class ConcreteState : IntermediateState
{
    public void Helper() { }
    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex) { }
}
", filePath: "Overrides.State.cs");
        var editorSource = UnitySource(@"
public abstract class IntermediateEditor : UnityEditor.Editor { }

public sealed class ConcreteEditor : IntermediateEditor
{
    public void Helper() { }
    public override void OnInspectorGUI() { }
}
", filePath: "Overrides.Editor.cs");
        var drawerSource = UnitySource(@"
public abstract class IntermediateDrawer : UnityEditor.PropertyDrawer { }

public sealed class ConcreteDrawer : IntermediateDrawer
{
    public void Helper() { }
    public override void OnGUI(
        UnityEngine.Rect position,
        UnityEditor.SerializedProperty property,
        UnityEngine.GUIContent label) { }
}
", filePath: "Overrides.Drawer.cs");
        var userSource = UnitySource(@"
public abstract class UserBehaviour : MonoBehaviour
{
    public virtual void OnUserCallback() { }
}

public sealed class UserOverride : UserBehaviour
{
    public override void OnUserCallback() { }
    public void Awake() { }
}
", filePath: "Overrides.User.cs");
        var objectSource = UnitySource(@"
public sealed class ObjectOverride : MonoBehaviour
{
    public override string ToString() { return ""ordinary""; }
    public void Awake() { }
}
", filePath: "Overrides.Object.cs");

        var diagnostics = await AnalyzeAsync(
            stateBaseSource,
            stateSource,
            editorSource,
            drawerSource,
            userSource,
            objectSource);

        AssertDiagnostics(
            diagnostics,
            Expect("DGUA002", stateSource, "OnStateEnter"),
            Expect("DGUA002", editorSource, "OnInspectorGUI"),
            Expect("DGUA002", drawerSource, "OnGUI"),
            Expect("DGUA002", userSource, "Awake"),
            Expect("DGUA002", objectSource, "Awake"));
    }

    [Fact]
    public async Task UnityInterfaceMethods_AreGroupedBeforeOrdinaryMethods()
    {
        var implicitSource = UnitySource(@"
public sealed class ImplicitInterfaces : MonoBehaviour,
    UnityEngine.ISerializationCallbackReceiver,
    UnityEngine.EventSystems.IPointerEnterHandler,
    UnityEngine.Playables.INotificationReceiver
{
    public void Helper() { }
    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize() { }
    public void OnPointerEnter(
        UnityEngine.EventSystems.PointerEventData eventData) { }
    public void OnNotify(
        UnityEngine.Playables.Playable origin,
        UnityEngine.Playables.INotification notification,
        object context) { }
}
", filePath: "Interfaces.Implicit.cs");
        var explicitSource = UnitySource(@"
public sealed class ExplicitInterfaces :
    UnityEngine.ISerializationCallbackReceiver,
    UnityEngine.EventSystems.IPointerEnterHandler,
    UnityEngine.Playables.INotificationReceiver
{
    public void Helper() { }

    void UnityEngine.ISerializationCallbackReceiver.OnBeforeSerialize() { }
    void UnityEngine.ISerializationCallbackReceiver.OnAfterDeserialize() { }
    void UnityEngine.EventSystems.IPointerEnterHandler.OnPointerEnter(
        UnityEngine.EventSystems.PointerEventData eventData) { }
    void UnityEngine.Playables.INotificationReceiver.OnNotify(
        UnityEngine.Playables.Playable origin,
        UnityEngine.Playables.INotification notification,
        object context) { }
}
", filePath: "Interfaces.Explicit.cs");
        var lookalikeSource = UnitySource(@"
public sealed class InterfaceLookalike : MonoBehaviour
{
    public void OnBeforeSerialize() { }
    public void Awake() { }
}
", filePath: "Interfaces.Lookalike.cs");

        var diagnostics = await AnalyzeAsync(
            implicitSource,
            explicitSource,
            lookalikeSource);

        AssertDiagnostics(
            diagnostics,
            Expect("DGUA002", implicitSource, "OnBeforeSerialize"),
            Expect("DGUA002", implicitSource, "OnAfterDeserialize"),
            Expect("DGUA002", implicitSource, "OnPointerEnter"),
            Expect("DGUA002", implicitSource, "OnNotify"),
            Expect("DGUA002", explicitSource, "OnBeforeSerialize"),
            Expect("DGUA002", explicitSource, "OnAfterDeserialize"),
            Expect("DGUA002", explicitSource, "OnPointerEnter"),
            Expect("DGUA002", explicitSource, "OnNotify"),
            Expect("DGUA002", lookalikeSource, "Awake"));
    }

    [Fact]
    public async Task NonUnityLookalikes_DoNotBecomeUnityCallbacks()
    {
        var plainSource = UnitySource(@"
public sealed class PlainLookalike
{
    public void Helper() { }
    public void Update() { }
    public void Awake() { }
}
", filePath: "Lookalikes.Plain.cs");
        var wrongNamespaceSource = UnitySource(@"
namespace Fake
{
    public sealed class Collision { }
}

public sealed class WrongParameterNamespace : MonoBehaviour
{
    public void OnCollisionEnter(Fake.Collision value) { }
    public void Awake() { }
}
", filePath: "Lookalikes.Namespace.cs");
        var wrongRefSource = UnitySource(@"
public sealed class WrongRefKind : MonoBehaviour
{
    public void OnAudioFilterRead(ref float[] data, int channels) { }
    public void Awake() { }
}
", filePath: "Lookalikes.Ref.cs");
        var genericSource = UnitySource(@"
public sealed class GenericImpostor : MonoBehaviour
{
    public void OnTriggerEnter<T>(Collider value) { }
    public void Awake() { }
}
", filePath: "Lookalikes.Generic.cs");
        var staticSource = UnitySource(@"
public sealed class StaticImpostor : MonoBehaviour
{
    public static void OnTriggerEnter(Collider value) { }
    public void Awake() { }
}
", filePath: "Lookalikes.Static.cs");
        var arbitrarySource = UnitySource(@"
public sealed class ArbitraryMessage : MonoBehaviour
{
    public void OnSomething() { }
    public void Awake() { }
}
", filePath: "Lookalikes.Arbitrary.cs");

        var diagnostics = await AnalyzeAsync(
            plainSource,
            wrongNamespaceSource,
            wrongRefSource,
            genericSource,
            staticSource,
            arbitrarySource);

        AssertDiagnostics(
            diagnostics,
            Expect("DGUA002", wrongNamespaceSource, "Awake"),
            Expect("DGUA002", wrongRefSource, "Awake"),
            Expect("DGUA002", genericSource, "Awake"),
            Expect("DGUA002", staticSource, "Awake"),
            Expect("DGUA002", arbitrarySource, "Awake"));
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
        Assert.True(
            expected.Length == actual.Length,
            "Expected " + expected.Length + " diagnostics but received: " +
            string.Join(
                ", ",
                actual.Select(diagnostic =>
                    diagnostic.Id + "@" +
                    diagnostic.Location.SourceTree?.FilePath + ":" +
                    diagnostic.Location.SourceSpan.Start)));

        foreach (var expectedDiagnostic in expected)
        {
            var expectedStart = FindIdentifier(
                expectedDiagnostic.Source.Text,
                expectedDiagnostic.Identifier);
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

            Assert.True(
                matches.Length == 1,
                "Expected exactly one " + expectedDiagnostic.Id + " for " +
                expectedDiagnostic.Identifier + " at character " + expectedStart +
                "; actual starts: " +
                string.Join(
                    ", ",
                    actual.Select(diagnostic =>
                        diagnostic.Id + "@" +
                        diagnostic.Location.SourceTree?.FilePath + ":" +
                        diagnostic.Location.SourceSpan.Start)));
            var diagnostic = matches[0];
            Assert.Equal(
                expectedDiagnostic.Identifier,
                expectedDiagnostic.Source.Text.Substring(
                    diagnostic.Location.SourceSpan.Start,
                    diagnostic.Location.SourceSpan.Length));
        }
    }

    private static int FindIdentifier(string text, string identifier)
    {
        var searchStart = 0;
        while (searchStart < text.Length)
        {
            var index = text.IndexOf(identifier, searchStart, StringComparison.Ordinal);
            if (index < 0)
            {
                return -1;
            }

            var beforeIsIdentifier = index > 0 && IsIdentifierCharacter(text[index - 1]);
            var afterIndex = index + identifier.Length;
            var afterIsIdentifier =
                afterIndex < text.Length && IsIdentifierCharacter(text[afterIndex]);
            if (!beforeIsIdentifier && !afterIsIdentifier)
            {
                return index;
            }

            searchStart = index + 1;
        }

        return -1;
    }

    private static bool IsIdentifierCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
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
