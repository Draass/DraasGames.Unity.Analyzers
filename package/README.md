# Project Overview

DraasGames.Unity.Analyzers is a set of Roslyn analyzers for keeping Unity code consistent across projects.
It checks serialized field grouping and Unity callback order, with settings for each project.

## Table of Contents

- [Dependencies](#dependencies)
- [Installation](#installation)
- [Rules that are implemented](#rules-that-are-implemented)
- [Configuration](#configuration)
- [Verification](#verification)
- [License](#license)

# Dependencies

- Unity 2022.3 or newer is the declared target; actual Unity import is not yet verified.
- Git is required when installing from a Git URL.

The compiled analyzer DLL is included. StyleCop and Odin are optional and are not dependencies of this package.
Your own scripts still need Odin if they use `[OdinSerialize]`.

# Installation

1. Open **Window > Package Manager**.
2. Select **Add package from git URL** and paste:

```text
https://github.com/Draass/DraasGames.Unity.Analyzers.git?path=/package#main
```

3. Wait for Unity to import the package and compile your scripts.

The URL uses the published `main` branch. There is currently no `v0.1.0` Git tag.
For a local checkout, use **Add package from disk** and select the `package.json` file in this folder.

# Rules that are implemented

## DGUA001 - Serialized field grouping

Fields with `[SerializeField]`, `[SerializeReference]`, or `[OdinSerialize]` should come before other instance fields.
You can configure them to come last. Static fields and constants are ignored.
Public fields without one of these attributes belong to the ordinary field group.

## DGUA002 - Unity callback order

Callbacks in subclasses of `MonoBehaviour` and `ScriptableObject` should follow the configured relative order.
The default order is:

```text
Reset > OnValidate > Awake > OnEnable > Start > FixedUpdate > Update > LateUpdate > OnDisable > OnDestroy
```

Missing callbacks are fine. Ordinary methods can appear between callbacks. `IEnumerator Start()` is supported on `MonoBehaviour`.
Only the callbacks supported by `ScriptableObject` are checked on that type.

Both rules are warnings by default. Generated code is ignored, and each type declaration is checked independently.
The rules check source layout, not Unity execution order. Automatic reordering fixes are not included.

# Configuration

### How to use

1. Copy [Settings.DraasGames.Unity.Analyzers.additionalfile](Documentation~/Examples/Settings.DraasGames.Unity.Analyzers.additionalfile)
   into your project's `Assets/Analyzers/` folder.
2. Set `draas_unity_serialized_field_placement` to `first` or `last`.
3. Set `draas_unity_callback_order` to a nonempty, duplicate-free subset or permutation of the supported callbacks.
4. Merge the [EditorConfig example](Documentation~/Examples/editorconfig.txt) into your root `.editorconfig` for IDE analysis.
5. Merge the [ruleset example](Documentation~/Examples/Default.ruleset) with your Unity ruleset to control diagnostic severity.

See [How to use and configure the package](Documentation~/usage.md) for full settings and rule behavior.
Review conflicting StyleCop ordering rules such as `SA1202` if your current conventions require a different field order.

# Verification

The initial implementation passed the Release build and all 15 approved Roslyn tests.
Unity import and Rider highlighting are not yet verified. See the [verification report](Documentation~/validation.md).

# License

The package is currently marked `UNLICENSED`. No open-source license has been granted.
