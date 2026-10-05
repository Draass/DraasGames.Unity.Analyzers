# How to use DraasGames.Unity.Analyzers

## Installation

In Unity Package Manager, select **Add package from git URL** and paste:

```text
https://github.com/Draass/DraasGames.Unity.Analyzers.git?path=/package#main
```

`Draass` is the GitHub owner, `/package` is the UPM package directory, and `main` is the published branch.
The package version is `0.1.0`; this does not imply that a matching Git tag exists. There is currently no `v0.1.0` tag.
Unity's [Git dependency documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-git.html#paths-and-revisions)
explains the path and revision syntax.

For a local checkout, select **Add package from disk** and choose `package/package.json` in the repository.
For a locally built archive, use **Add package from tarball**.

The prebuilt DLL is labeled `RoslynAnalyzer`, and its runtime plugin import is disabled.
It is outside any assembly definition that would restrict it to one package assembly.
Check the analyzer's actual scope when integrating it into projects with custom assembly definitions.

## Configuration

### Unity compiler settings

Copy [Settings.DraasGames.Unity.Analyzers.additionalfile](Examples/Settings.DraasGames.Unity.Analyzers.additionalfile)
into `Assets/Analyzers/` in the consuming Unity project:

```ini
draas_unity_serialized_field_placement = first
draas_unity_callback_order = Reset,OnValidate,Awake,OnEnable,Start,FixedUpdate,Update,LateUpdate,OnDisable,OnDestroy
```

- `draas_unity_serialized_field_placement` accepts `first` or `last`.
- `draas_unity_callback_order` accepts a nonempty, duplicate-free subset or permutation of the ten names above.
- Callback names and option keys are case-sensitive. Placement values must be lowercase.
- Callbacks omitted from the configured list are not checked.
- An invalid value falls back to the built-in default for that option.
- Blank lines and lines starting with `#` or `;` are ignored. Unknown keys are ignored.

The filename must use the format `Name.DraasGames.Unity.Analyzers.additionalfile`, with no dots in the `Name` part.
Unity uses the analyzer assembly name to route additional files to the compiler.
See [Unity's additional-file documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/roslyn-analyzers-additional-files.html).

### IDE settings and severity

Merge the [EditorConfig example](Examples/editorconfig.txt) into the project's root `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.DGUA001.severity = warning
dotnet_diagnostic.DGUA002.severity = warning
draas_unity_serialized_field_placement = first
draas_unity_callback_order = Reset,OnValidate,Awake,OnEnable,Start,FixedUpdate,Update,LateUpdate,OnDisable,OnDestroy
```

Use `severity = none` to disable an individual rule in the IDE.
For Unity compiler severity, merge the [ruleset example](Examples/Default.ruleset) into the project's ruleset.
Use `Action="None"` to disable a rule there. Preserve existing settings instead of replacing the file.

### Settings precedence

Settings are resolved in this order; later values override earlier values:

1. Built-in defaults.
2. Matching additional files, sorted by their full paths using ordinal comparison.
3. Per-file EditorConfig options, when the host supplies them to Roslyn.

Within one additional file, the last occurrence of a key wins. Use one settings file per project for predictable results.
An invalid value in a later layer falls back to the built-in default rather than the value from an earlier layer.
Do not rely on EditorConfig alone for Unity compiler settings: configuration support depends on the host.
Keep IDE settings and the Unity additional file aligned.

## Rule behavior

### DGUA001 - Serialized field grouping

The analyzer resolves these attributes by their full semantic names:

- `UnityEngine.SerializeField`
- `UnityEngine.SerializeReference`
- `Sirenix.Serialization.OdinSerializeAttribute`

C# aliases work. Unrelated attributes with the same short name are ignored.
Static fields and constants do not participate in grouping. Public fields without an explicit marker are ordinary fields.
The rule checks explicit markers, not actual serializability: `readonly` and `[NonSerialized]` do not remove a grouping marker.
Each violating field declaration gets one diagnostic on its first variable identifier.

### DGUA002 - Unity callback order

The analyzer checks direct and indirect subclasses of `UnityEngine.MonoBehaviour` and `UnityEngine.ScriptableObject`.
For `ScriptableObject`, only `OnValidate`, `Awake`, `OnEnable`, `OnDisable`, and `OnDestroy` are recognized.

Recognized callbacks must be nonstatic, nongeneric, parameterless methods with an implementation and a `void` return type.
`MonoBehaviour.Start` can also return `System.Collections.IEnumerator` by value.
Abstract methods, ref returns, and incompatible signatures are ignored.

Callbacks only need to follow the configured relative order. Ordinary methods do not break the sequence.
A callback that appears after a higher-ranked callback receives a diagnostic on its name.

### Shared behavior

Generated code is excluded. Nested types and partial declarations are checked independently; the analyzer does not impose an order across files.
There are no automatic source-reordering fixes in this version, because moving fields can change initializer execution order.

## Using StyleCop alongside this package

StyleCop can continue to check general C# style. Check for ordering conflicts before enabling overlapping rules.
For example, `SA1202` can require a public ordinary field before a private serialized field, while `DGUA001` with `first` requires the opposite.
Configure or disable only the conflicting rule in the appropriate scope. This package does not change StyleCop settings automatically.

## Verification

The initial implementation passed all 15 approved Roslyn tests and the Release build.
Actual Unity import and Rider highlighting are not yet verified. See the [verification report](validation.md) for the scope of those checks.
