# Project Overview
Roslyn analyzers that keep Unity scripts laid out the same way across projects. Serialized fields go
in one group, Unity methods go before your own methods, and every project can tune the rules to its
own code style.

## Table of Contents
- [Dependencies](#dependencies)
- [Installation](#installation)
- [Rules that are implemented](#rules-that-are-implemented)
    - [DGUA001 - Serialized field grouping](#dgua001---serialized-field-grouping)
    - [DGUA002 - Unity callback order](#dgua002---unity-callback-order)
    - [IDE quick-fixes](#ide-quick-fixes)
- [Configuration](#configuration)
- [Assembly scope](#assembly-scope)
- [Known limitations](#known-limitations)
- [License](#license)

# Dependencies
There are 2 types of dependencies: absolute must and optional

## Absolute must dependencies
- Unity 2022.3 or newer — the declared target, actual Unity import is not verified yet
- Git — only if you install from a Git URL

The analyzer DLL ships prebuilt.

## Optional dependencies
- StyleCop Analyzers — can stay installed. Review ordering rules like `SA1202` if they disagree with your field order
- Odin Inspector — only if your own scripts use `[OdinSerialize]`. The analyzer does not reference Odin

# Installation
1. Open **Window → Package Manager**
2. Select **Add package from git URL** and paste https://github.com/Draass/DraasGames.Unity.Analyzers.git?path=/package#main
- The URL points at the `main` branch, no release tag required
- For a local checkout, use **Add package from disk** and select the `package.json` in this folder
3. Wait for Unity to import the package and recompile scripts
4. Enjoy

# Rules that are implemented
1. DGUA001 — serialized fields are grouped together
2. DGUA002 — Unity methods come before ordinary methods and keep a configurable lifecycle order
3. IDE quick-fixes for both rules, including Fix All

Both rules are warnings by default. They check source layout, not Unity execution order. Generated code
is ignored and each type declaration is checked independently.

## DGUA001 - Serialized field grouping
Fields with `[SerializeField]`, `[SerializeReference]` or `[OdinSerialize]` should come before the other
instance fields. You can configure them to come last instead. Static fields and constants are ignored,
public fields without one of these attributes belong to the ordinary group.

## DGUA002 - Unity callback order
All recognized Unity messages, Unity API overrides and Unity interface implementations must appear before
ordinary methods. These ten lifecycle names also keep a configurable relative order:

``` text
Reset > OnValidate > Awake > OnEnable > Start > FixedUpdate > Update > LateUpdate > OnDisable > OnDestroy
```

- `Awake()` after `InstallBindings()` reports `DGUA002`, same as `OnTriggerEnter` or `OnGUI` below helper methods
- Missing callbacks are fine
- Leaving a callback out of the relative-order list does not exempt it from grouping
- `draas_unity_callbacks_before_other_methods = false` checks only the relative order

Recognized message hosts, signatures and limits are listed in the [callback catalog](Documentation~/unity-callbacks.md).

## IDE quick-fixes
In Rider, press **Alt+Enter** on DGUA001 (**Group serialized fields**) or DGUA002 (**Order Unity methods**).
**Fix All** works per document, project or solution. The fixes use the same settings as the diagnostics
and keep attributes, comments and method bodies.

### How to use
1. Enable Roslyn analyzers in Rider's inspection settings
2. After installing or updating this package, regenerate project files in Unity's **External Tools** preferences
3. Let Rider reload the solution
4. Done

The package connects a separate IDE code-fix DLL to projects that already use the analyzer. Saving or
formatting a file does not apply the fixes. See [IDE integration](Documentation~/ide-integration.md).

# Configuration
### How to use
1. Copy [Settings.DraasGames.Unity.Analyzers.additionalfile](Documentation~/Examples/Settings.DraasGames.Unity.Analyzers.additionalfile)
   into your project's `Assets/Analyzers/` folder
2. Set `draas_unity_serialized_field_placement` to `first` or `last`
3. Set `draas_unity_callback_order` to a nonempty, duplicate-free subset or permutation of the supported callbacks
4. Keep `draas_unity_callbacks_before_other_methods = true` to place callbacks before ordinary methods, or set it to `false` for relative order only
5. Merge the [EditorConfig example](Documentation~/Examples/editorconfig.txt) into your root `.editorconfig` for IDE analysis
6. Merge the [ruleset example](Documentation~/Examples/Default.ruleset) with your Unity ruleset to control severity

Full settings and rule behavior are in [How to use and configure the package](Documentation~/usage.md).

# Assembly scope
Use `Assets/Default.ruleset` for defaults and a `.ruleset` next to a target `.asmdef` for per-assembly
overrides. Works the same with StyleCop. Full example is in [Assembly scope](Documentation~/assembly-scope.md).

# Known limitations
1. Types with directives or syntax errors are left unchanged by the fixes
2. Field fixes skip structs, types with `StructLayout` and reorders that change instance initializer order,
   so a warning may stay without a safe automatic fix
3. 0.2.0 passed the strict Release build with no warnings or errors and all 39 approved tests. Unity import
   and the Rider Alt+Enter/Fix All UI are not verified yet — see the [verification report](Documentation~/validation.md)

# License
The package is currently marked `UNLICENSED`, no open-source license has been granted.
The adapted message catalog has its own [third-party license notice](Third%20Party%20Notices.md).
