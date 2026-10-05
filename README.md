# Project Overview

DraasGames.Unity.Analyzers is a set of Roslyn analyzers for keeping Unity code consistent across projects.
It checks serialized field grouping and Unity callback order. Each project can configure the rules to match its own coding style.

The repository includes the analyzer source, a prebuilt Unity Package Manager package, and configuration examples.

## Table of Contents

- [Dependencies](#dependencies)
- [Installation](#installation)
- [Rules that are implemented](#rules-that-are-implemented)
- [Configuration](#configuration)
- [Building from source](#building-from-source)
- [Verification](#verification)
- [License](#license)

# Dependencies

## Required dependencies

- Unity 2022.3 or newer is the package's declared target. See [Verification](#verification) for the actual checks performed.
- Git must be available to Unity when installing from a Git URL.

The compiled analyzer DLL is included. You do not need a .NET SDK to install the package in a Unity project.

## Optional dependencies

- StyleCop Analyzers can remain installed for general C# style rules.
- Odin is only needed if your own scripts use `[OdinSerialize]`. The analyzer recognizes the attribute without taking a dependency on Odin.

# Installation

## From Git URL

1. Open **Window > Package Manager** in Unity.
2. Select **Add package from git URL**.
3. Paste this URL:

```text
https://github.com/Draass/DraasGames.Unity.Analyzers.git?path=/package#main
```

4. Wait for Unity to import the package and compile your scripts.
5. Add project-specific settings if you want to change the defaults.

The URL uses the published `main` branch. The package version is `0.1.0`, but there is currently no `v0.1.0` Git tag.
Use the URL above as written. The GitHub owner is `Draass`, and the UPM package is in the `package` subfolder.

## From a local checkout

1. Select **Add package from disk** in Package Manager.
2. Select `package/package.json` inside this repository.

For the existing checkout on this machine, the file is:

```text
C:/Programming/Unity/DraasGames.Unity.Analyzers/package/package.json
```

You can also use **Add package from tarball** with an archive produced by the build script.

# Rules that are implemented

Both rules are enabled as warnings by default. They check declaration order in the source file and do not change Unity execution order.

## DGUA001 - Serialized field grouping

Fields marked with `[SerializeField]`, `[SerializeReference]`, or `[OdinSerialize]` should appear before other instance fields.
Set `draas_unity_serialized_field_placement = last` if you prefer them at the end.

### How to use

```csharp
using UnityEngine;

public sealed class PlayerView : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float speed;

    private float elapsedTime;
    private bool isMoving;
}
```

Static fields and constants are ignored. Public fields without an explicit serialization attribute belong to the ordinary field group.
The rule checks attribute-based grouping, not whether Unity or Odin can actually serialize a field.

## DGUA002 - Unity callback order

Unity callbacks should follow this relative order by default:

```text
Reset > OnValidate > Awake > OnEnable > Start > FixedUpdate > Update > LateUpdate > OnDisable > OnDestroy
```

### How to use

Keep the callbacks you use in that order. You do not need to declare missing callbacks, and ordinary methods can appear between them.
Change `draas_unity_callback_order` to use a different order or to check only a subset of these callbacks.

The rule recognizes subclasses of `MonoBehaviour` and `ScriptableObject`, including indirect subclasses.
It supports `IEnumerator Start()` on `MonoBehaviour` and ignores methods whose signatures do not match supported callbacks.

Both rules ignore generated code and check nested types and partial declarations independently.
There are no automatic reordering fixes in this version.

# Configuration

For Unity compilation, copy [Settings.DraasGames.Unity.Analyzers.additionalfile](package/Documentation~/Examples/Settings.DraasGames.Unity.Analyzers.additionalfile)
into your project's `Assets/Analyzers/` folder:

```ini
draas_unity_serialized_field_placement = first
draas_unity_callback_order = Reset,OnValidate,Awake,OnEnable,Start,FixedUpdate,Update,LateUpdate,OnDisable,OnDestroy
```

For IDE analysis, merge the [EditorConfig example](package/Documentation~/Examples/editorconfig.txt) into your root `.editorconfig`.
For Unity diagnostic severity, merge the [ruleset example](package/Documentation~/Examples/Default.ruleset) with your existing ruleset.

See [How to use and configure the package](package/Documentation~/usage.md) for precedence, supported signatures, rule exclusions, and StyleCop compatibility.

# Building from source

You need .NET SDK 8, PowerShell, and `tar`. Run these commands from the repository root:

```powershell
./scripts/Build-Package.ps1
dotnet test ./tests/DraasGames.Unity.Analyzers.Tests/DraasGames.Unity.Analyzers.Tests.csproj --configuration Release
```

The build script updates `package/Analyzers/DraasGames.Unity.Analyzers.dll` and creates
`artifacts/com.draasgames.unity-analyzers-0.1.0.tgz`. Include the prebuilt DLL and its `.meta` file when publishing a package update to Git.

# Verification

The initial implementation passed a Release build with no warnings or errors and all 15 approved Roslyn tests.
Import into an actual Unity project and Rider highlighting have not yet been verified.
See the [verification report](docs/validation.md) for details.

# License

The package is currently marked `UNLICENSED`. No open-source license has been granted.
