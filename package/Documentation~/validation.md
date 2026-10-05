# Validation — 2026-10-05

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

Local repository is initialized on main; no files have been staged, committed, pushed or published.
Detailed local execution and final archive hash are in artifacts/verification.md (build artifacts are ignored by Git).

## Package metadata repair — 2026-10-05

The consumer reported that Unity ignored README.md because its immutable package copy had no .meta file.
Package inspection also found missing metadata for CHANGELOG.md and package.json. Added the three metadata
files with distinct stable GUIDs and matching text/package-manifest importers. Existing analyzer GUIDs are preserved.
Build-Package.ps1 now checks metadata coverage before creating the archive, excluding Unity-ignored folders such as Documentation~.

Validation for this repair is limited to package metadata, archive contents and unchanged analyzer DLL hashes.
The .NET tests were not rerun; the analyzer source did not change. Unity reimport after this repair is not yet verified.
