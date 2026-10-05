# IDE integration

Version 0.2.0 ships two assemblies beside one another in `package/Analyzers/`:

- `DraasGames.Unity.Analyzers.dll` is the compiler analyzer. Its existing `RoslynAnalyzer` label and
  disabled Unity runtime import are unchanged.
- `DraasGames.Unity.Analyzers.CodeFixes.dll` is loaded by an IDE that supports Roslyn code fixes. Its
  Unity plugin metadata disables every runtime and Editor platform import and deliberately has no
  `RoslynAnalyzer` label. Roslyn Workspaces and MEF assemblies are supplied by the IDE; they are not
  copied into the Unity package.

The package's Editor-only assembly contains `CodeFixProjectPostprocessor`. Unity and the Rider project
generator call its public static `OnGeneratedCSProject(string path, string content)` callback while writing
each generated C# project. The callback:

1. Resolves the installed package through `PackageInfo.FindForAssetPath` and uses its `resolvedPath`, so
   local, embedded, registry and Git package layouts do not depend on a checkout-relative path.
2. Returns the generated XML unchanged when the code-fix DLL is absent or when the project does not already
   contain an `<Analyzer Include="...DraasGames.Unity.Analyzers.dll" />` item.
3. Adds one `<Analyzer Include="...DraasGames.Unity.Analyzers.CodeFixes.dll" />` item to the existing
   analyzer item group. It preserves the MSBuild namespace, existing items and escaped/Unicode paths;
   repeated generation is deduplicated. Malformed XML is returned unchanged.

The hook is scoped to project files that already analyze with the base DLL. It does not add an ordinary
assembly reference, change project references, or affect Unity's compiler analyzer loading.

## Why this callback is used

Unity documents `AssetPostprocessor.OnGeneratedCSProject` as the extension point for changing generated
project files:

- [Customize project files created by VSTU](https://learn.microsoft.com/en-us/visualstudio/gamedev/unity/extensibility/customize-project-files-created-by-vstu)
- [Unity `AssetPostprocessor` reference](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/assetpostprocessor)

The Rider package uses the same callback contract. Its project generator discovers static methods named
`OnGeneratedCSProject`, passes `(path, content)`, and consumes the returned string. It emits Roslyn
analyzers as `<Analyzer Include="..." />` items. The implementation is visible in the maintained package
source mirror at [ProjectGeneration.cs](https://github.com/needle-mirror/com.unity.ide.rider/blob/master/Rider/Editor/ProjectGeneration/ProjectGeneration.cs),
especially the callback dispatch and analyzer item generation. The package is maintained for the Rider
Unity integration described by [JetBrains' Unity support repository](https://github.com/JetBrains/resharper-unity).

Package lookup follows Unity's documented API: [PackageInfo.FindForAssetPath](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/packagemanager/packageinfo/findforassetpath)
returns package information for assets under `Packages/`, including Git and local packages, and
`resolvedPath` identifies the package directory on disk.

## Build and reinstall

From the repository root, run:

```powershell
./scripts/Build-Package.ps1 -Configuration Release
```

The script builds the code-fix project (which references the analyzer project), copies both resulting
`netstandard2.0` DLLs into `package/Analyzers/`, checks Unity metadata coverage, and writes
`artifacts/com.draasgames.unity-analyzers-0.2.0.tgz`. Only the two product DLLs are copied; Workspaces,
MEF and other build-time dependencies remain IDE-provided.

After installing the package from Git, disk or tarball, use the Unity Editor's external-tools command to
regenerate project files (for example, Rider's **Regenerate project files** action). Reinstall or rebuild
the package when changing the packaged DLLs, then regenerate the projects so the new analyzer item is
present. A generated project that does not already contain the base analyzer is intentionally left alone.

The hook is exercised through its public callback using narrow Unity API stubs, including scope,
deduplication, namespaces, escaped paths and malformed XML. Code-fix tests use MEF discovery and the
public Roslyn APIs. A real Unity import and a real Rider code-fix session remain separate integration
checks; these tests do not claim either host has executed the integration.
