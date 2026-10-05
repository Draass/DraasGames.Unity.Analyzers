# Validation — 2026-10-05

## 0.2.0 — IDE code fixes

Sixteen new tests plus the existing 23 diagnostic tests were explicitly approved on 2026-10-05.

- Strict .NET Release solution build: passed, 0 warnings, 0 errors, with warnings treated as errors.
- Approved suite: 39 discovered, 39 passed, 0 failed; the original 23 test methods are unchanged.
- New coverage: public Roslyn code actions, MEF provider discovery, document/project/solution Fix All,
  configuration, cancellation, declaration/trivia preservation, initializer/layout/directive safety,
  and the actual project-generation callback compiled with narrow Unity API stubs.
- Both product assemblies are version 0.2.0.0, target netstandard2.0 and use the Roslyn 3.8 API baseline.
- Analyzer DLL SHA256: 8C87040C0F53C51619E66718638A4AC50E83138173F4A77838DBC5B45C1A6ADE.
- Code-fix DLL SHA256: 624D80DD3625419022791FEC6086B30F648D1270C8AF687106E2C9D84792F857.
- Actual Unity package import, generated-project hook execution inside Unity, and Rider Alt+Enter/Fix All UI:
  not verified. The public API tests and stubbed hook checks do not replace these host checks.

The strict build caught a nullable-flow warning in the Fix All equivalence key; it was corrected locally.
Earlier warning-nonfatal diagnostic runs are historical only. The final strict build and complete 39-test
run used the repaired source. No blanket nullable-warning suppression was added to the test project.
Raw final logs, DLL and packaging evidence are retained locally under artifacts/0.2.0/.

Source/package integration is described in [IDE integration](ide-integration.md). The code-fix DLL uses
IDE-provided Workspaces/MEF; it is not a Unity compiler analyzer or a runtime plugin.

## 0.1.2 — all Unity methods

The user clarified that all Unity methods should precede ordinary methods. Placement is independent
of the configured relative-order subset. The implementation includes runtime/Editor message metadata
and semantic Unity override/interface recognition.

- Test approval: all 23 methods in the consolidated specification table approved on 2026-10-05.
- .NET Release solution build: passed, 0 warnings, 0 errors.
- Behavioral Roslyn suite: 23 discovered, 23 passed, 0 failed.
- Baseline regression: UnityCallbackAfterOrdinaryMethod_Reports failed as expected against preserved
  0.1.0 source at d29d80f (2 diagnostics instead of the required 5).
- Release analyzer assembly version: 0.1.2.0; netstandard2.0; Microsoft.CodeAnalysis 3.8 API baseline.
- Release DLL SHA256: 1F0A9769AF3E2C8C88EBC436AD3616400B11242033F4FDCAB94D4DA8F6616E2B.
- Actual consumer Unity import and Rider highlighting: not verified.

The first current-tree run found an abstract convention-only callback recognition regression and a
fixture identifier-position issue. Both were corrected; the final Release build and all 23 tests passed.
Tests cover public Roslyn diagnostics with handwritten semantic stubs, not a complete Unity API/runtime matrix.
Raw build, baseline, test and packaging evidence is retained locally under artifacts/0.1.2/.

The older verification records below are historical and do not establish acceptance of 0.1.2.

## 0.1.1 — callback placement extension

DGUA002 now checks that configured Unity callbacks appear before ordinary methods by default.
The new draas_unity_callbacks_before_other_methods option can restore relative-order-only behavior.

- Production source review: completed against the extension contract in specification.md.
- .NET Release analyzer build: passed, 0 warnings, 0 errors; assembly version 0.1.1.0.
- Extension tests were pending at this intermediate stage; the approved 0.1.2 suite supersedes that proposal.
- Updated DLL and UPM archive: packaged as com.draasgames.unity-analyzers-0.1.1.tgz; behavioral acceptance is still pending.
- Unity import and Rider diagnostics after this change: not verified.

The baseline results below describe version 0.1.0 and do not validate the new default behavior.

## 0.1.0 — baseline verification

The two requested ordering rules and their reusable UPM distribution are implemented.
The user approved all 15 test methods listed in specification.md.

- .NET Release solution compilation: passed, 0 warnings, 0 errors.
- Behavioral Roslyn tests: 15 discovered, 15 passed, 0 failed.
- UPM archive and DLL metadata inspection: passed. Release and packaged DLL hashes match.
- Analyzer assembly version: 0.1.0.0; netstandard2.0; Microsoft.CodeAnalysis 3.8 API baseline.
- Packaged DLL SHA256: B036DFBC1604DD7EEBB61B33A18026AFE6D0B01D72583B11752DFC0F2C6983A3.
- Actual Unity 2022.3 / Unity 6 import and compilation: not performed.
- Rider highlighting inside a consumer Unity project: not performed.
- ConstructionSimulator integration: not performed.

Do not interpret the package manifest's minimum Unity version as a completed compatibility matrix.

## Reproducible commands

```powershell
dotnet build ./DraasGames.Unity.Analyzers.sln --configuration Release
dotnet test ./DraasGames.Unity.Analyzers.sln --configuration Release --no-build --filter "FullyQualifiedName~DraasGames.Unity.Analyzers.Tests.OrderingAnalyzerTests" --logger "console;verbosity=normal"
./scripts/Build-Package.ps1 -Configuration Release -SkipBuild
```

The tests use semantic Unity/Odin stubs with the public Roslyn analyzer host. They verify diagnostic
IDs, counts and identifier locations, false-positive boundaries, configuration and suppression.
They do not validate actual Unity serialization or runtime execution.

Two production nullable errors were corrected during the initial build. Test harness typing and
one invalid fixture were corrected within the approved test list. The final source passed the build
and complete approved suite. No Unity project was modified to obtain these results.

The original implementation was prepared locally without staging, committing or publishing it on the user's behalf.
The historical 0.1.0 execution and archive hash are in artifacts/verification.md (build artifacts are ignored by Git).

## Package metadata repair — 2026-10-05

The consumer reported that Unity ignored README.md because its immutable package copy had no .meta file.
Package inspection also found missing metadata for CHANGELOG.md and package.json. Added the three metadata
files with distinct stable GUIDs and matching text/package-manifest importers. Existing analyzer GUIDs are preserved.
Build-Package.ps1 now checks metadata coverage before creating the archive, excluding Unity-ignored folders such as Documentation~.

Validation for this repair is limited to package metadata, archive contents and unchanged analyzer DLL hashes.
The .NET tests were not rerun; the analyzer source did not change. Unity reimport after this repair is not yet verified.
