# Unity method catalog

`UnityCallbackClassifier` recognizes Unity classes and methods from Roslyn symbols.  The analyzer
does not reference UnityEngine, UnityEditor, or any other Unity runtime assembly, and it never
loads a Unity file while analyzing a compilation.

The catalog contains 164 method signatures across the ten callback hosts identified by
Microsoft.Unity.Analyzers' `ScriptInfo.Types` list:

| Host | Catalog entries | Static entries |
| --- | ---: | ---: |
| `UnityEngine.MonoBehaviour` | 72 | 0 |
| `UnityEngine.ScriptableObject` | 6 | 0 |
| `UnityEngine.StateMachineBehaviour` | 7 | 0 |
| `UnityEngine.EventSystems.UIBehaviour` | 14 | 0 |
| `UnityEngine.Networking.NetworkBehaviour` | 11 | 0 |
| `UnityEditor.Editor` | 5 | 0 |
| `UnityEditor.EditorWindow` | 12 | 0 |
| `UnityEditor.ScriptableWizard` | 3 | 0 |
| `UnityEditor.AssetPostprocessor` | 31 | 5 |
| `UnityEditor.AssetImporters.ScriptedImporter` | 3 | 1 |

The MonoBehaviour group includes the current Unity 6 lifecycle, application, animation, audio,
input, rendering, particle, transform/UI, and physics messages, as well as the legacy network and
level-loading messages present in the reference metadata.  The other groups include their
declared inherited messages.  Non-void contracts such as `Editor.CreateInspectorGUI`,
`Editor.OnGetFrameBounds`, `NetworkBehaviour.OnSerialize`, `AssetPostprocessor.OnAssignMaterialModel`,
and `ScriptedImporter.GatherDependenciesFromSourceFile` are represented explicitly.  Static
AssetPostprocessor and ScriptedImporter callbacks are separate signatures, so an instance method
with the same name does not match.

For physics messages whose Unity documentation permits discarding the event data, the catalog
accepts the exact declared parameter prefix or the parameterless form.  Supplied parameters still
need the documented type and `ref` kind.  Other messages require their complete catalog parameter
list.  Coroutine-capable MonoBehaviour messages accept `void`, `System.Collections.IEnumerator`,
and the awaitable forms represented by the reference analyzer metadata.

In addition to catalog matching, the classifier recognizes an override chain whose Unity-origin
member is declared in `UnityEngine.*` or `UnityEditor.*`, and an actual implementation (including
an explicit implementation) of a Unity-namespaced interface.  A `System.Object.ToString` override
alone is therefore ordinary, while a Unity-origin `ToString` contract is recognized.  A method's
name or namespace by itself is never enough; `Zenject.InstallBindings` remains ordinary.  A class
derived from a UnityEngine or UnityEditor base symbol is in the Unity type scope even when that
base is an extension host such as `PropertyDrawer`, `DecoratorDrawer`, or `PlayableBehaviour`;
their methods are recognized through their actual Unity-origin override contracts.

## Sources and scope

The signature data is adapted from the callback host declarations in
[UnityStubs.cs at commit fd250c39](https://raw.githubusercontent.com/microsoft/Microsoft.Unity.Analyzers/fd250c39b858921df0f9479926338b0bad6259fb/src/Microsoft.Unity.Analyzers/UnityStubs.cs)
from [Microsoft.Unity.Analyzers](https://github.com/microsoft/Microsoft.Unity.Analyzers),
licensed under the MIT License.  The repository records the required attribution separately in
`package/Third Party Notices.md`.

The lifecycle and message names were checked against the Unity 6 scripting API pages for
[MonoBehaviour](https://docs.unity3d.com/6000.0/ScriptReference/MonoBehaviour.html),
[ScriptableObject](https://docs.unity3d.com/6000.0/ScriptReference/ScriptableObject.html),
[Editor](https://docs.unity3d.com/6000.0/ScriptReference/Editor.html),
[EditorWindow](https://docs.unity3d.com/6000.0/ScriptReference/EditorWindow.html),
[AssetPostprocessor](https://docs.unity3d.com/6000.0/ScriptReference/AssetPostprocessor.html),
and [ScriptedImporter](https://docs.unity3d.com/6000.0/ScriptReference/ScriptedImporter.html).
The Unity 6 `ScriptableObject` list is the reason `Reset` is included for that host.

This is a maintained catalog of the selected callback hosts, rather than a promise to cover every
future Unity API or every package-defined callback.  A message introduced after this catalog's
reference revision is still recognized when the consumer's method is an actual Unity-origin
override or Unity-namespaced interface implementation.  A package callback that is only a
convention and has no such semantic contract requires a catalog update.  Methods on ordinary
classes, coincidentally named `On*` methods, unrelated interfaces, and runtime APIs such as
`KeyCode` are outside the catalog.
