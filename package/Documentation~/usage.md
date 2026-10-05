# How to use DraasGames.Unity.Analyzers

## Installation

In Unity Package Manager, select **Add package from git URL** and paste:

```text
https://github.com/Draass/DraasGames.Unity.Analyzers.git?path=/package#main
```

`Draass` is the GitHub owner, `/package` is the UPM package directory, and `main` is the published branch.
The URL uses a branch rather than a version tag; package versions and Git tags are separate.
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
draas_unity_callbacks_before_other_methods = true
draas_unity_callback_order = Reset,OnValidate,Awake,OnEnable,Start,FixedUpdate,Update,LateUpdate,OnDisable,OnDestroy
```

- `draas_unity_serialized_field_placement` accepts `first` or `last`.
- `draas_unity_callbacks_before_other_methods` accepts `true` (default) or `false`, ignoring surrounding whitespace and letter case.
- `draas_unity_callback_order` accepts a nonempty, duplicate-free subset or permutation of the ten names above.
- Callback names and option keys are case-sensitive. Placement values must be lowercase.
- The configured list controls relative ordering of the ten lifecycle names only. Every recognized Unity method
  still participates in the leading group when `draas_unity_callbacks_before_other_methods` is true.
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
draas_unity_callbacks_before_other_methods = true
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

The analyzer uses a semantic callback classifier covering runtime and Editor message hosts, Unity API overrides,
and Unity interface implementations. It checks full parameter/return types and the appropriate static/instance form.
This includes parameterized physics, audio, rendering and application messages, serialization callbacks,
EventSystems handlers and Editor callbacks. See the [callback catalog](unity-callbacks.md) for exact coverage and limits.

By default, every recognized Unity method must precede ordinary methods. Ranked lifecycle callbacks must also follow the configured relative order.
For example, this declaration order reports DGUA002 on `Awake`:

```csharp
private void BindConstruction() { }
private void Awake() { } // Move before BindConstruction.
```

Instance, static, generic, override, explicit-interface, and abstract ordinary methods form a boundary unless
recognized as actual Unity methods. Fields, constructors, properties, and members of nested types do not.
Methods with invalid callback signatures are ordinary methods. Unity methods omitted from `draas_unity_callback_order`
remain subject to grouping and have no effect on relative rank. Zenject/user API overrides remain ordinary;
an override is recognized through its actual Unity-declared contract, not merely because its class inherits MonoBehaviour.

Set `draas_unity_callbacks_before_other_methods = false` to restore the previous behavior and allow helper methods
before or between callbacks. Relative callback inversions still report DGUA002.
Each offending callback receives at most one diagnostic on its name. A placement violation takes priority
over a callback-order violation when both apply; the message identifies the method it should precede.

### Shared behavior

Generated code is excluded. Nested types and partial declarations are checked independently; the analyzer does not impose an order across files.
IDE quick-fixes reorder eligible declarations explicitly, subject to the safety limits below.

## IDE quick-fixes

Use Rider's **Alt+Enter** on DGUA001 to **Group serialized fields**, or on DGUA002 to **Order Unity methods**.
Each action fixes the selected type declaration. **Fix All** processes the selected diagnostic in a document,
project or solution. It uses each document's effective settings and does not change suppressed diagnostics.

Field fixes perform a stable first/last grouping of instance field declaration slots. Static fields,
constants and non-field members retain their slots. Method fixes preserve non-method slots, group Unity
methods when enabled, and sort ranked callbacks within ranked slots. Ordinary methods and unranked callbacks
retain their relative order. With callback placement disabled, helper/unranked method slots stay unchanged.

Attributes, comments, XML documentation and method bodies move with their declaration. No changes cross
partial declarations or enclosing/nested types. Fix All handles nested declarations independently.

Fixes are withheld for types containing directives (`#if`, `#region`, `#pragma`, `#nullable`) or syntax errors.
The field action is also withheld for structs, types explicitly marked with `StructLayout`, and any permutation
that changes the relative order of existing instance field, property or field-like event initializers.
It does not guess whether initializers are pure. For example, moving an uninitialized field past an initialized
field can be safe, while swapping two initialized fields is not automatically offered.

Unsafe declarations retain their diagnostics; Fix All can still fix safe declarations elsewhere.
These actions do not run automatically on save or ordinary formatting. After a package update, regenerate
IDE project files and reload Rider. See [IDE integration](ide-integration.md) for details.

## Using StyleCop alongside this package

StyleCop can continue to check general C# style. Check for ordering conflicts before enabling overlapping rules.
For example, `SA1202` can require a public ordinary field before a private serialized field, while `DGUA001` with `first` requires the opposite.
The same accessibility preference can conflict with private callbacks placed before public helper methods.
Static-first method ordering can also conflict with DGUA002; use the placement option to match the project's policy.
Configure or disable only the conflicting rule in the appropriate scope. This package does not change StyleCop settings automatically.

## Verification

For per-assembly configuration, see [Assembly scope](assembly-scope.md).

Version 0.2.0 passed the strict Release build and all 39 approved diagnostic/code-fix/integration tests.
Actual Unity import and Rider Alt+Enter/Fix All UI are not yet verified. See the [verification report](validation.md) for the scope of those checks.
