# Repository instructions

This is a standalone .NET Roslyn analyzer repository, not a Unity game project or a ConstructionSimulator worktree.
Normal filesystem/CLI tools are permitted here. Read README.md and docs/specification.md before changing behavior.

- Keep the analyzer compatible with netstandard2.0 and the Microsoft.CodeAnalysis 3.8 API baseline.
- Do not add UnityEngine, Odin, Workspaces or StyleCop runtime dependencies to the analyzer DLL.
- Preserve semantic symbol recognition, per-declaration ordering and cancellation; avoid filesystem reads inside analyzers.
- Keep user-visible diagnostics and configuration contracts documented. No automatic consumer-project mutations.
- Keep source ownership exclusive during agent work. Build/test execution has one owner at a time.
- Before writing, changing or running tests beyond an already approved scope, present exact names, scenarios,
  expected results and actions to the user and wait for explicit approval.
  The 15 methods in docs/specification.md were approved for addition and execution on 2026-10-05.
- Verify behavior through public Roslyn diagnostics rather than private helper assertions or source text matching.
- After changing analyzer source, rebuild the packaged DLL and archive with scripts/Build-Package.ps1.
- Record real verification in docs/validation.md. .NET tests do not prove Unity import or IDE integration.
- Do not stage, commit, push, publish, or modify consumer projects without a user request.
