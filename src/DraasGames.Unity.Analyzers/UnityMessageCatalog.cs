using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DraasGames.Unity.Analyzers
{
    /// <summary>
    /// The small, dependency-free description of the Unity message surface used by the
    /// callback ordering analyzer.  The names in this table are metadata names rather than
    /// references to Unity types, so the analyzer can run in a compilation which does not
    /// reference the Unity assemblies itself.
    /// </summary>
    internal static class UnityMessageCatalog
    {
        internal const string MonoBehaviour = "UnityEngine.MonoBehaviour";
        internal const string ScriptableObject = "UnityEngine.ScriptableObject";
        internal const string StateMachineBehaviour = "UnityEngine.StateMachineBehaviour";
        internal const string UIBehaviour = "UnityEngine.EventSystems.UIBehaviour";
        internal const string NetworkBehaviour = "UnityEngine.Networking.NetworkBehaviour";
        internal const string Editor = "UnityEditor.Editor";
        internal const string EditorWindow = "UnityEditor.EditorWindow";
        internal const string ScriptableWizard = "UnityEditor.ScriptableWizard";
        internal const string AssetPostprocessor = "UnityEditor.AssetPostprocessor";
        internal const string ScriptedImporter = "UnityEditor.AssetImporters.ScriptedImporter";

        internal static readonly ImmutableArray<string> KnownHosts = ImmutableArray.Create(
            MonoBehaviour,
            ScriptableObject,
            StateMachineBehaviour,
            UIBehaviour,
            NetworkBehaviour,
            Editor,
            EditorWindow,
            ScriptableWizard,
            AssetPostprocessor,
            ScriptedImporter);

        internal static readonly ImmutableArray<UnityMethodSignature> Methods = ImmutableArray.Create(
            // ScriptableObject messages.  Reset is intentionally included: Unity 6 documents
            // it as a ScriptableObject message even though it is easy to overlook in older API
            // lists.
            Message(ScriptableObject, "Awake", UnityReturnKind.Void),
            Message(ScriptableObject, "OnDestroy", UnityReturnKind.Void),
            Message(ScriptableObject, "OnDisable", UnityReturnKind.Void),
            Message(ScriptableObject, "OnEnable", UnityReturnKind.Void),
            Message(ScriptableObject, "OnValidate", UnityReturnKind.Void),
            Message(ScriptableObject, "Reset", UnityReturnKind.Void),

            // MonoBehaviour lifecycle, application, rendering, input, physics and UI messages.
            Message(MonoBehaviour, "Awake", UnityReturnKind.Void),
            Message(MonoBehaviour, "FixedUpdate", UnityReturnKind.Void),
            Message(MonoBehaviour, "LateUpdate", UnityReturnKind.Void),
            CoroutineMessage(MonoBehaviour, "OnApplicationPause", Parameter("System.Boolean")),
            CoroutineMessage(MonoBehaviour, "OnApplicationQuit"),
            CoroutineMessage(MonoBehaviour, "OnBecameInvisible"),
            CoroutineMessage(MonoBehaviour, "OnBecameVisible"),
            PhysicsCoroutineMessage(MonoBehaviour, "OnCollisionEnter", Parameter("UnityEngine.Collision")),
            PhysicsCoroutineMessage(MonoBehaviour, "OnCollisionExit", Parameter("UnityEngine.Collision")),
            PhysicsCoroutineMessage(MonoBehaviour, "OnCollisionStay", Parameter("UnityEngine.Collision")),
            Message(MonoBehaviour, "OnDisable", UnityReturnKind.Void),
            CoroutineMessage(MonoBehaviour, "OnDrawGizmos"),
            CoroutineMessage(MonoBehaviour, "OnDrawGizmosSelected"),
            Message(MonoBehaviour, "OnEnable", UnityReturnKind.Void),
            CoroutineMessage(MonoBehaviour, "OnLevelWasLoaded", Parameter("System.Int32")),
            CoroutineMessage(MonoBehaviour, "OnMouseDown"),
            CoroutineMessage(MonoBehaviour, "OnMouseDrag"),
            CoroutineMessage(MonoBehaviour, "OnMouseEnter"),
            CoroutineMessage(MonoBehaviour, "OnMouseExit"),
            CoroutineMessage(MonoBehaviour, "OnMouseOver"),
            CoroutineMessage(MonoBehaviour, "OnMouseUp"),
            CoroutineMessage(MonoBehaviour, "OnParticleCollision", Parameter("UnityEngine.GameObject")),
            CoroutineMessage(MonoBehaviour, "OnPostRender"),
            CoroutineMessage(MonoBehaviour, "OnPreCull"),
            CoroutineMessage(MonoBehaviour, "OnPreRender"),
            CoroutineMessage(
                MonoBehaviour,
                "OnRenderImage",
                Parameter("UnityEngine.RenderTexture"),
                Parameter("UnityEngine.RenderTexture")),
            PhysicsCoroutineMessage(MonoBehaviour, "OnTriggerEnter", Parameter("UnityEngine.Collider")),
            PhysicsCoroutineMessage(MonoBehaviour, "OnTriggerExit", Parameter("UnityEngine.Collider")),
            PhysicsCoroutineMessage(MonoBehaviour, "OnTriggerStay", Parameter("UnityEngine.Collider")),
            CoroutineMessage(MonoBehaviour, "Reset"),
            CoroutineMessage(MonoBehaviour, "Start"),
            Message(MonoBehaviour, "Update", UnityReturnKind.Void),
            CoroutineMessage(MonoBehaviour, "OnConnectedToServer"),
            PhysicsCoroutineMessage(
                MonoBehaviour,
                "OnControllerColliderHit",
                Parameter("UnityEngine.ControllerColliderHit")),
            Message(
                MonoBehaviour,
                "OnDisconnectedFromServer",
                UnityReturnKind.Void,
                Parameter("UnityEngine.NetworkDisconnection")),
            Message(
                MonoBehaviour,
                "OnDisconnectedFromMasterServer",
                UnityReturnKind.Void,
                Parameter("UnityEngine.NetworkDisconnection")),
            Message(
                MonoBehaviour,
                "OnFailedToConnect",
                UnityReturnKind.Void,
                Parameter("UnityEngine.NetworkConnectionError")),
            Message(
                MonoBehaviour,
                "OnFailedToConnectToMasterServer",
                UnityReturnKind.Void,
                Parameter("UnityEngine.NetworkConnectionError")),
            Message(MonoBehaviour, "OnGUI", UnityReturnKind.Void),
            PhysicsCoroutineMessage(MonoBehaviour, "OnJointBreak", Parameter("System.Single")),
            Message(
                MonoBehaviour,
                "OnMasterServerEvent",
                UnityReturnKind.Void,
                Parameter("UnityEngine.MasterServerEvent")),
            Message(
                MonoBehaviour,
                "OnNetworkInstantiate",
                UnityReturnKind.Void,
                Parameter("UnityEngine.NetworkMessageInfo")),
            Message(
                MonoBehaviour,
                "OnPlayerConnected",
                UnityReturnKind.Void,
                Parameter("UnityEngine.NetworkPlayer")),
            Message(
                MonoBehaviour,
                "OnPlayerDisconnected",
                UnityReturnKind.Void,
                Parameter("UnityEngine.NetworkPlayer")),
            Message(
                MonoBehaviour,
                "OnSerializeNetworkView",
                UnityReturnKind.Void,
                Parameter("UnityEngine.BitStream"),
                Parameter("UnityEngine.NetworkMessageInfo")),
            CoroutineMessage(MonoBehaviour, "OnServerInitialized"),
            CoroutineMessage(MonoBehaviour, "OnWillRenderObject"),
            CoroutineMessage(MonoBehaviour, "OnApplicationFocus", Parameter("System.Boolean")),
            Message(MonoBehaviour, "OnRenderObject", UnityReturnKind.Void),
            Message(MonoBehaviour, "OnDestroy", UnityReturnKind.Void),
            CoroutineMessage(MonoBehaviour, "OnMouseUpAsButton"),
            CoroutineMessage(
                MonoBehaviour,
                "OnAudioFilterRead",
                Parameter("System.Single[]"),
                Parameter("System.Int32")),
            CoroutineMessage(MonoBehaviour, "OnAnimatorIK", Parameter("System.Int32")),
            CoroutineMessage(MonoBehaviour, "OnAnimatorMove"),
            Message(MonoBehaviour, "OnValidate", UnityReturnKind.Void),
            PhysicsCoroutineMessage(
                MonoBehaviour,
                "OnCollisionEnter2D",
                Parameter("UnityEngine.Collision2D")),
            PhysicsCoroutineMessage(
                MonoBehaviour,
                "OnCollisionExit2D",
                Parameter("UnityEngine.Collision2D")),
            PhysicsCoroutineMessage(
                MonoBehaviour,
                "OnCollisionStay2D",
                Parameter("UnityEngine.Collision2D")),
            PhysicsCoroutineMessage(
                MonoBehaviour,
                "OnTriggerEnter2D",
                Parameter("UnityEngine.Collider2D")),
            PhysicsCoroutineMessage(
                MonoBehaviour,
                "OnTriggerExit2D",
                Parameter("UnityEngine.Collider2D")),
            PhysicsCoroutineMessage(
                MonoBehaviour,
                "OnTriggerStay2D",
                Parameter("UnityEngine.Collider2D")),
            PhysicsCoroutineMessage(MonoBehaviour, "OnJointBreak2D", Parameter("UnityEngine.Joint2D")),
            CoroutineMessage(MonoBehaviour, "OnBeforeTransformParentChanged"),
            CoroutineMessage(MonoBehaviour, "OnTransformParentChanged"),
            CoroutineMessage(MonoBehaviour, "OnTransformChildrenChanged"),
            CoroutineMessage(MonoBehaviour, "OnRectTransformDimensionsChange"),
            CoroutineMessage(MonoBehaviour, "OnRectTransformRemoved"),
            CoroutineMessage(MonoBehaviour, "OnCanvasGroupChanged"),
            CoroutineMessage(MonoBehaviour, "OnParticleTrigger"),
            CoroutineMessage(MonoBehaviour, "OnParticleSystemStopped"),
            CoroutineMessage(MonoBehaviour, "OnParticleUpdateJobScheduled"),
            CoroutineMessage(MonoBehaviour, "OnChildRectTransformDimensionsChange"),

            // StateMachineBehaviour messages.
            Message(
                StateMachineBehaviour,
                "OnStateEnter",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Animator"),
                Parameter("UnityEngine.AnimatorStateInfo"),
                Parameter("System.Int32")),
            Message(
                StateMachineBehaviour,
                "OnStateExit",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Animator"),
                Parameter("UnityEngine.AnimatorStateInfo"),
                Parameter("System.Int32")),
            Message(
                StateMachineBehaviour,
                "OnStateIK",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Animator"),
                Parameter("UnityEngine.AnimatorStateInfo"),
                Parameter("System.Int32")),
            Message(
                StateMachineBehaviour,
                "OnStateMove",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Animator"),
                Parameter("UnityEngine.AnimatorStateInfo"),
                Parameter("System.Int32")),
            Message(
                StateMachineBehaviour,
                "OnStateUpdate",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Animator"),
                Parameter("UnityEngine.AnimatorStateInfo"),
                Parameter("System.Int32")),
            Message(
                StateMachineBehaviour,
                "OnStateMachineEnter",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Animator"),
                Parameter("System.Int32")),
            Message(
                StateMachineBehaviour,
                "OnStateMachineExit",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Animator"),
                Parameter("System.Int32")),

            // UIBehaviour messages.
            Message(UIBehaviour, "Awake", UnityReturnKind.Void),
            Message(UIBehaviour, "OnEnable", UnityReturnKind.Void),
            Message(UIBehaviour, "Start", UnityReturnKind.Void),
            Message(UIBehaviour, "OnDisable", UnityReturnKind.Void),
            Message(UIBehaviour, "OnDestroy", UnityReturnKind.Void),
            Message(UIBehaviour, "IsActive", UnityReturnKind.Boolean),
            Message(UIBehaviour, "OnValidate", UnityReturnKind.Void),
            Message(UIBehaviour, "Reset", UnityReturnKind.Void),
            Message(UIBehaviour, "OnRectTransformDimensionsChange", UnityReturnKind.Void),
            Message(UIBehaviour, "OnBeforeTransformParentChanged", UnityReturnKind.Void),
            Message(UIBehaviour, "OnTransformParentChanged", UnityReturnKind.Void),
            Message(UIBehaviour, "OnDidApplyAnimationProperties", UnityReturnKind.Void),
            Message(UIBehaviour, "OnCanvasGroupChanged", UnityReturnKind.Void),
            Message(UIBehaviour, "OnCanvasHierarchyChanged", UnityReturnKind.Void),

            // Legacy NetworkBehaviour messages.
            Message(
                NetworkBehaviour,
                "OnCheckObserver",
                UnityReturnKind.Boolean,
                Parameter("UnityEngine.Networking.NetworkConnection")),
            Message(
                NetworkBehaviour,
                "OnDeserialize",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Networking.NetworkReader"),
                Parameter("System.Boolean")),
            Message(NetworkBehaviour, "OnNetworkDestroy", UnityReturnKind.Void),
            Message(
                NetworkBehaviour,
                "OnRebuildObservers",
                UnityReturnKind.Boolean,
                Parameter(
                    UnityTypeSignature.Generic(
                        "System.Collections.Generic.HashSet`1",
                        UnityTypeSignature.Named("UnityEngine.Networking.NetworkConnection"))),
                Parameter("System.Boolean")),
            Message(
                NetworkBehaviour,
                "OnSerialize",
                UnityReturnKind.Boolean,
                Parameter("UnityEngine.Networking.NetworkWriter"),
                Parameter("System.Boolean")),
            Message(
                NetworkBehaviour,
                "OnSetLocalVisibility",
                UnityReturnKind.Void,
                Parameter("System.Boolean")),
            Message(NetworkBehaviour, "OnStartAuthority", UnityReturnKind.Void),
            Message(NetworkBehaviour, "OnStartClient", UnityReturnKind.Void),
            Message(NetworkBehaviour, "OnStartLocalPlayer", UnityReturnKind.Void),
            Message(NetworkBehaviour, "OnStartServer", UnityReturnKind.Void),
            Message(NetworkBehaviour, "OnStopAuthority", UnityReturnKind.Void),

            // UnityEditor.Editor callbacks, including their non-void contracts.
            Message(Editor, "CreateInspectorGUI", UnityReturnKind.VisualElement),
            Message(Editor, "ShouldHideOpenButton", UnityReturnKind.Boolean),
            Message(Editor, "OnSceneGUI", UnityReturnKind.Void),
            Message(Editor, "HasFrameBounds", UnityReturnKind.Boolean),
            Message(Editor, "OnGetFrameBounds", UnityReturnKind.Bounds),

            // UnityEditor.EditorWindow callbacks.
            Message(EditorWindow, "OnFocus", UnityReturnKind.Void),
            Message(EditorWindow, "OnDestroy", UnityReturnKind.Void),
            Message(EditorWindow, "OnGUI", UnityReturnKind.Void),
            Message(EditorWindow, "OnHierarchyChange", UnityReturnKind.Void),
            Message(EditorWindow, "OnInspectorUpdate", UnityReturnKind.Void),
            Message(EditorWindow, "OnLostFocus", UnityReturnKind.Void),
            Message(EditorWindow, "OnProjectChange", UnityReturnKind.Void),
            Message(EditorWindow, "OnSelectionChange", UnityReturnKind.Void),
            Message(EditorWindow, "Update", UnityReturnKind.Void),
            Message(EditorWindow, "CreateGUI", UnityReturnKind.Void),
            Message(EditorWindow, "OnBecameVisible", UnityReturnKind.Void),
            Message(EditorWindow, "OnBecameInvisible", UnityReturnKind.Void),

            // UnityEditor.ScriptableWizard callbacks.
            Message(ScriptableWizard, "OnWizardUpdate", UnityReturnKind.Void),
            Message(ScriptableWizard, "OnWizardCreate", UnityReturnKind.Void),
            Message(ScriptableWizard, "OnWizardOtherButton", UnityReturnKind.Void),

            // UnityEditor.AssetPostprocessor callbacks.  These are instance callbacks unless
            // explicitly marked static below; static-ness is part of the signature.
            Message(
                AssetPostprocessor,
                "OnAssignMaterialModel",
                UnityReturnKind.Material,
                Parameter("UnityEngine.Material"),
                Parameter("UnityEngine.Renderer")),
            Message(
                AssetPostprocessor,
                "OnPostprocessAnimation",
                UnityReturnKind.Void,
                Parameter("UnityEngine.GameObject"),
                Parameter("UnityEngine.AnimationClip")),
            Message(
                AssetPostprocessor,
                "OnPostprocessAssetbundleNameChanged",
                UnityReturnKind.Void,
                Parameter("System.String"),
                Parameter("System.String"),
                Parameter("System.String")),
            Message(
                AssetPostprocessor,
                "OnPostprocessAudio",
                UnityReturnKind.Void,
                Parameter("UnityEngine.AudioClip")),
            Message(
                AssetPostprocessor,
                "OnPostprocessCubemap",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Cubemap")),
            Message(
                AssetPostprocessor,
                "OnPostprocessGameObjectWithAnimatedUserProperties",
                UnityReturnKind.Void,
                Parameter("UnityEngine.GameObject"),
                Parameter("UnityEditor.EditorCurveBinding[]")),
            Message(
                AssetPostprocessor,
                "OnPostprocessGameObjectWithUserProperties",
                UnityReturnKind.Void,
                Parameter("UnityEngine.GameObject"),
                Parameter("System.String[]"),
                Parameter("System.Object[]")),
            Message(
                AssetPostprocessor,
                "OnPostprocessMaterial",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Material")),
            Message(
                AssetPostprocessor,
                "OnPostprocessMeshHierarchy",
                UnityReturnKind.Void,
                Parameter("UnityEngine.GameObject")),
            Message(
                AssetPostprocessor,
                "OnPostprocessModel",
                UnityReturnKind.Void,
                Parameter("UnityEngine.GameObject")),
            Message(
                AssetPostprocessor,
                "OnPostprocessSpeedTree",
                UnityReturnKind.Void,
                Parameter("UnityEngine.GameObject")),
            Message(
                AssetPostprocessor,
                "OnPostprocessSprites",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Texture2D"),
                Parameter("UnityEngine.Sprite[]")),
            Message(
                AssetPostprocessor,
                "OnPostprocessTexture",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Texture2D")),
            Message(
                AssetPostprocessor,
                "OnPostprocessPrefab",
                UnityReturnKind.Void,
                Parameter("UnityEngine.GameObject")),
            Message(
                AssetPostprocessor,
                "OnPostprocessTexture2DArray",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Texture2DArray")),
            Message(
                AssetPostprocessor,
                "OnPostprocessTexture3D",
                UnityReturnKind.Void,
                Parameter("UnityEngine.Texture3D")),
            Message(
                AssetPostprocessor,
                "OnProcessScene",
                UnityReturnKind.Void,
                Parameter("UnityEngine.SceneManagement.Scene"),
                Parameter("UnityEditor.Build.Content.SceneImportContext")),
            Message(AssetPostprocessor, "OnPreprocessAnimation", UnityReturnKind.Void),
            Message(AssetPostprocessor, "OnPreprocessAsset", UnityReturnKind.Void),
            Message(AssetPostprocessor, "OnPreprocessAudio", UnityReturnKind.Void),
            Message(
                AssetPostprocessor,
                "OnPreprocessMaterialDescription",
                UnityReturnKind.Void,
                Parameter("UnityEditor.AssetImporters.MaterialDescription"),
                Parameter("UnityEngine.Material"),
                Parameter("UnityEngine.AnimationClip[]")),
            Message(AssetPostprocessor, "OnPreprocessModel", UnityReturnKind.Void),
            Message(AssetPostprocessor, "OnPreprocessSpeedTree", UnityReturnKind.Void),
            Message(AssetPostprocessor, "OnPreprocessTexture", UnityReturnKind.Void),
            Message(
                AssetPostprocessor,
                "OnPreprocessCameraDescription",
                UnityReturnKind.Void,
                Parameter("UnityEditor.AssetImporters.CameraDescription"),
                Parameter("UnityEngine.Camera"),
                Parameter("UnityEngine.AnimationClip[]")),
            Message(
                AssetPostprocessor,
                "OnPreprocessLightDescription",
                UnityReturnKind.Void,
                Parameter("UnityEditor.AssetImporters.LightDescription"),
                Parameter("UnityEngine.Light"),
                Parameter("UnityEngine.AnimationClip[]")),
            StaticMessage(
                AssetPostprocessor,
                "OnPostprocessAllAssets",
                UnityReturnKind.Void,
                Parameter("System.String[]"),
                Parameter("System.String[]"),
                Parameter("System.String[]"),
                Parameter("System.String[]")),
            StaticMessage(
                AssetPostprocessor,
                "OnPostprocessAllAssets",
                UnityReturnKind.Void,
                Parameter("System.String[]"),
                Parameter("System.String[]"),
                Parameter("System.String[]"),
                Parameter("System.String[]"),
                Parameter("System.Boolean")),
            StaticMessage(AssetPostprocessor, "OnPreGeneratingCSProjectFiles", UnityReturnKind.Boolean),
            StaticMessage(
                AssetPostprocessor,
                "OnGeneratedSlnSolution",
                UnityReturnKind.String,
                Parameter("System.String"),
                Parameter("System.String")),
            StaticMessage(
                AssetPostprocessor,
                "OnGeneratedCSProject",
                UnityReturnKind.String,
                Parameter("System.String"),
                Parameter("System.String")),

            // UnityEditor.AssetImporters.ScriptedImporter callbacks.
            StaticMessage(
                ScriptedImporter,
                "GatherDependenciesFromSourceFile",
                UnityReturnKind.StringArray,
                Parameter("System.String")),
            Message(ScriptedImporter, "OnValidate", UnityReturnKind.Void),
            Message(ScriptedImporter, "Reset", UnityReturnKind.Void));

        internal static IEnumerable<UnityMethodSignature> GetMethods(
            string hostMetadataName,
            string methodName)
        {
            foreach (UnityMethodSignature method in Methods)
            {
                if (string.Equals(method.HostMetadataName, hostMetadataName, StringComparison.Ordinal)
                    && string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    yield return method;
                }
            }
        }

        internal static bool IsKnownHost(INamedTypeSymbol typeSymbol)
        {
            string metadataName = GetMetadataName(typeSymbol);
            foreach (string knownHost in KnownHosts)
            {
                if (string.Equals(metadataName, knownHost, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetMetadataName(INamedTypeSymbol typeSymbol)
        {
            var typeNames = new System.Collections.Generic.List<string>();
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

        private static UnityMethodSignature Message(
            string host,
            string name,
            UnityReturnKind returnKind,
            params UnityParameterSignature[] parameters)
        {
            return new UnityMethodSignature(
                host,
                name,
                returnKind,
                isStatic: false,
                allowOmittedTrailingParameters: false,
                parameters: parameters);
        }

        private static UnityMethodSignature CoroutineMessage(
            string host,
            string name,
            params UnityParameterSignature[] parameters)
        {
            return new UnityMethodSignature(
                host,
                name,
                UnityReturnKind.Coroutine,
                isStatic: false,
                allowOmittedTrailingParameters: false,
                parameters: parameters);
        }

        private static UnityMethodSignature PhysicsCoroutineMessage(
            string host,
            string name,
            params UnityParameterSignature[] parameters)
        {
            return new UnityMethodSignature(
                host,
                name,
                UnityReturnKind.Coroutine,
                isStatic: false,
                allowOmittedTrailingParameters: true,
                parameters: parameters);
        }

        private static UnityMethodSignature StaticMessage(
            string host,
            string name,
            UnityReturnKind returnKind,
            params UnityParameterSignature[] parameters)
        {
            return new UnityMethodSignature(
                host,
                name,
                returnKind,
                isStatic: true,
                allowOmittedTrailingParameters: false,
                parameters: parameters);
        }

        private static UnityParameterSignature Parameter(string metadataName)
        {
            return new UnityParameterSignature(UnityTypeSignature.Named(metadataName), RefKind.None);
        }

        private static UnityParameterSignature Parameter(UnityTypeSignature type)
        {
            return new UnityParameterSignature(type, RefKind.None);
        }
    }

    internal enum UnityReturnKind
    {
        Void,
        Coroutine,
        Boolean,
        String,
        Material,
        Bounds,
        VisualElement,
        StringArray
    }

    internal readonly struct UnityMethodSignature
    {
        internal UnityMethodSignature(
            string hostMetadataName,
            string name,
            UnityReturnKind returnKind,
            bool isStatic,
            bool allowOmittedTrailingParameters,
            params UnityParameterSignature[] parameters)
        {
            HostMetadataName = hostMetadataName;
            Name = name;
            ReturnKind = returnKind;
            IsStatic = isStatic;
            AllowOmittedTrailingParameters = allowOmittedTrailingParameters;
            Parameters = ImmutableArray.CreateRange(parameters);
        }

        internal string HostMetadataName { get; }

        internal string Name { get; }

        internal UnityReturnKind ReturnKind { get; }

        internal bool IsStatic { get; }

        internal bool AllowOmittedTrailingParameters { get; }

        internal ImmutableArray<UnityParameterSignature> Parameters { get; }
    }

    internal readonly struct UnityParameterSignature
    {
        internal UnityParameterSignature(UnityTypeSignature type, RefKind refKind)
        {
            Type = type;
            RefKind = refKind;
        }

        internal UnityTypeSignature Type { get; }

        internal RefKind RefKind { get; }
    }

    internal readonly struct UnityTypeSignature
    {
        private UnityTypeSignature(
            string metadataName,
            bool isArray,
            ImmutableArray<UnityTypeSignature> typeArguments)
        {
            MetadataName = metadataName;
            IsArray = isArray;
            TypeArguments = typeArguments;
        }

        internal string MetadataName { get; }

        internal bool IsArray { get; }

        internal ImmutableArray<UnityTypeSignature> TypeArguments { get; }

        internal static UnityTypeSignature Named(string metadataName)
        {
            if (metadataName.EndsWith("[]", StringComparison.Ordinal))
            {
                return Array(metadataName.Substring(0, metadataName.Length - 2));
            }

            return new UnityTypeSignature(
                metadataName,
                isArray: false,
                ImmutableArray<UnityTypeSignature>.Empty);
        }

        internal static UnityTypeSignature Generic(
            string metadataName,
            params UnityTypeSignature[] typeArguments)
        {
            return new UnityTypeSignature(
                metadataName,
                isArray: false,
                ImmutableArray.CreateRange(typeArguments));
        }

        private static UnityTypeSignature Array(string elementMetadataName)
        {
            return new UnityTypeSignature(
                elementMetadataName,
                isArray: true,
                ImmutableArray<UnityTypeSignature>.Empty);
        }
    }
}
