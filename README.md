# Project Overview
Roslyn analyzers that keep Unity scripts laid out the same way across projects. Serialized fields go
in one group, Unity methods go before your own methods, and every project can tune the rules to its
own code style — without dragging Unity, Odin or StyleCop into the analyzer itself.

The repository contains the analyzer source, a prebuilt Unity Package Manager package and
configuration examples.

## Table of Contents
- [Dependencies](#dependencies)
- [Installation](#installation)
- [Rules that are implemented](#rules-that-are-implemented)
    - [DGUA001 - Serialized field grouping](#dgua001---serialized-field-grouping)
    - [DGUA002 - Unity callback order](#dgua002---unity-callback-order)
    - [IDE quick-fixes](#ide-quick-fixes)
- [Configuration](#configuration)
- [Assembly scope](#assembly-scope)
- [Building from source](#building-from-source)
- [Known limitations](#known-limitations)
- [TODO](#todo)
- [License](#license)

# Dependencies
There are 2 types of dependencies: absolute must and optional

## Absolute must dependencies
- Unity 2022.3 or newer — the declared target in `package.json`. See [Known limitations](#known-limitations)
  for what has actually been checked
- Git — only if you install from a Git URL

The analyzer DLL ships prebuilt, so you don't need a .NET SDK to use the package in Unity.

## Optional dependencies
- StyleCop Analyzers — can stay installed for general C# style rules. Watch out for ordering rules
  like `SA1202` that may disagree with your field order
- Odin Inspector — only needed if your own scripts use `[OdinSerialize]`. The analyzer recognizes the
  attribute by name and does not reference Odin

# Installation
1. Open **Window → Package Manager**
2. Select **Add package from git URL** and paste https://github.com/Draass/DraasGames.Unity.Analyzers.git?path=/package#main
- The URL points at the `main` branch, no release tag required
- The UPM package lives in the `package` subfolder, `?path=` picks only it
- For a local checkout, use **Add package from disk** and select `package/package.json`, or
  **Add package from tarball** with an archive produced by the [build script](#building-from-source)
3. Wait for Unity to import the package and recompile scripts
4. Optionally add project settings, see [Configuration](#configuration)
5. Enjoy

# Rules that are implemented
1. DGUA001 — serialized fields are grouped together
2. DGUA002 — Unity methods come before ordinary methods and keep a configurable lifecycle order
3. IDE quick-fixes for both rules, including Fix All

Both rules are warnings by default. They check declaration order in the source file and have nothing
to do with Unity execution order. Generated code is ignored, nested types and partial declarations are
checked independently.

## DGUA001 - Serialized field grouping
Fields marked with `[SerializeField]`, `[SerializeReference]` or `[OdinSerialize]` should come before
the other instance fields.

### How to use
``` csharp
using UnityEngine;

public sealed class PlayerView : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float speed;

    private float elapsedTime;
    private bool isMoving;
}
```

- Prefer them at the end? Set `draas_unity_serialized_field_placement = last`
- Static fields and constants are ignored
- Public fields without an explicit serialization attribute belong to the ordinary group

The rule checks attributes, not whether Unity or Odin can actually serialize the field.

## DGUA002 - Unity callback order
Every recognized Unity method must appear before ordinary methods such as binding helpers or init
routines. On top of that, these ten lifecycle callbacks keep a relative order:

``` text
Reset > OnValidate > Awake > OnEnable > Start > FixedUpdate > Update > LateUpdate > OnDisable > OnDestroy
```

### How to use
1. Put Unity methods at the top of the method list, in the order above
2. Fields, constructors and properties can still go before them
3. You don't have to declare missing callbacks
4. Done

So `Awake()` after `InstallBindings()` reports `DGUA002`, even if it is the only callback in the class.
The same goes for unranked callbacks like `OnTriggerEnter` or `OnGUI` sitting below helper methods.
Each callback gets at most one warning.

### Settings
- `draas_unity_callback_order` — a different order or a subset of the ten names above. It controls
  relative order only: physics, rendering, GUI, audio and application messages still belong before
  ordinary methods even if they are not in the list
- `draas_unity_callbacks_before_other_methods = false` — allows ordinary methods before or between Unity
  methods, while still checking the relative order

### What counts as a Unity method
Unity runtime and Editor message hosts and their subclasses, real overrides of Unity APIs and
implementations of Unity interfaces (serialization callbacks, EventSystems handlers and so on). Full
signatures are checked semantically, so a random method named `OnSomething` is not a Unity callback.
Covered types, signature sources and limits are in [the callback catalog](docs/unity-callbacks.md).

## IDE quick-fixes
Added in 0.2.0. Both rules get a Roslyn code fix that reorders the type declaration for you.

| Action | Fixes | Uses |
|---|---|---|
| **Group serialized fields** | DGUA001 | configured `first` / `last` placement |
| **Order Unity methods** | DGUA002 | configured method grouping and relative order |
| **Fix All** | selected rule | document, project or solution, when the IDE supports it |

### How to use
1. In Rider, enable Roslyn analyzers under **Settings → Editor → Inspection Settings → Roslyn Analyzers**
2. After installing or updating the package, regenerate project files in Unity's **External Tools**
   preferences and let Rider reload the solution
3. Put the caret on a warning and press **Alt+Enter**

The fix keeps attributes, comments, method bodies, untouched members and the relative order of ordinary
and unranked Unity methods. Fix All skips unsafe declarations and processes the rest. These are explicit
actions — saving or formatting a file does not trigger them.

The code-fix DLL is separate from the compiler analyzer. An Editor-only project-generation hook connects
it to IDE projects that already use the analyzer, so Unity's compiler never sees it. See
[IDE integration](docs/ide-integration.md) for the packaging details.

# Configuration
For Unity compilation, copy [Settings.DraasGames.Unity.Analyzers.additionalfile](package/Documentation~/Examples/Settings.DraasGames.Unity.Analyzers.additionalfile)
into your project's `Assets/Analyzers/` folder:

``` ini
draas_unity_serialized_field_placement = first
draas_unity_callbacks_before_other_methods = true
draas_unity_callback_order = Reset,OnValidate,Awake,OnEnable,Start,FixedUpdate,Update,LateUpdate,OnDisable,OnDestroy
```

- For IDE analysis, merge the [EditorConfig example](package/Documentation~/Examples/editorconfig.txt) into your root `.editorconfig`
- For Unity diagnostic severity, merge the [ruleset example](package/Documentation~/Examples/Default.ruleset) with your existing ruleset

Precedence, supported signatures, rule exclusions and StyleCop compatibility are covered in
[How to use and configure the package](package/Documentation~/usage.md).

# Assembly scope
Use `Assets/Default.ruleset` for the project-wide policy. Override it with a `.ruleset` next to the target
`.asmdef`, or with `Assets/Assembly-CSharp.ruleset` for the predefined Assembly-CSharp. Works the same for
DGUA rules and StyleCop's SA rules.

A local ruleset alone does not disable rules elsewhere. To enable the rules only for a few assemblies,
disable them in the default policy and enable them next to the target assembly definitions. Full example
is in [Assembly scope](package/Documentation~/assembly-scope.md).

# Building from source
You need .NET SDK 8, PowerShell and `tar`. From the repository root:

``` powershell
./scripts/Build-Package.ps1
dotnet test ./tests/DraasGames.Unity.Analyzers.Tests/DraasGames.Unity.Analyzers.Tests.csproj --configuration Release
```

The script updates the prebuilt DLLs in `package/Analyzers/` and creates
`artifacts/com.draasgames.unity-analyzers-0.2.0.tgz`. When publishing a package update to Git, include
both DLLs, their `.meta` files and the `Editor` integration folder.

# Known limitations
1. No automatic fix for types containing preprocessor / region directives or syntax errors
2. The field fix also skips structs, types with `StructLayout`, and reorders that would change the order
   of instance field, property or event initializers. The warning stays, so you can sort it out by hand
3. 0.2.0 passed the strict Release build with no warnings or errors and all 39 approved tests.
   Import into a real Unity project and the Rider Alt+Enter/Fix All UI are not verified yet — details
   in the [verification report](docs/validation.md)

# TODO
1. Verify import and compilation in a real Unity project
2. Verify Rider highlighting and quick-fixes inside a consumer project

# License
The package is currently marked `UNLICENSED`, no open-source license has been granted.
The adapted Unity message catalog is covered separately by the [Third Party Notices](package/Third%20Party%20Notices.md).
