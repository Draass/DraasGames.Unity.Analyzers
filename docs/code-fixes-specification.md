# IDE code fixes — 0.2.0

## Authorization and checkout

2026-10-05: the user requested adding the proposed Alt+Enter code fixes for DGUA001/DGUA002 and Fix All.
Authoritative checkout: C:/Programming/Unity/DraasGames.Unity.Analyzers. Do not modify consumer projects,
stage, commit or publish. Existing analyzer behavior/configuration and the 23 approved diagnostic tests remain.
This is explicit IDE source editing; automatic editing on save is outside this feature.

## Architecture and dependencies

- Add a separate netstandard2.0 DraasGames.Unity.Analyzers.CodeFixes assembly using Roslyn
  Microsoft.CodeAnalysis.CSharp.Workspaces 3.8.0 and MEF export attributes. Reference the analyzer project
  and share its internal settings, callback classification and serialized marker predicates through
  InternalsVisibleTo for the production code-fix assembly only. No test-only production access.
- Keep Workspaces/MEF/Unity dependencies out of the compiler analyzer DLL.
- Bundle the IDE DLL alongside the existing analyzer with platform imports disabled and NO RoslynAnalyzer
  label on the IDE DLL. An Editor-only project-generation hook adds its Analyzer reference only to generated
  projects already referencing DraasGames.Unity.Analyzers.dll. Do not alter analyzer scope or ordinary references.
  Verify this route against the Rider editor package source; report actual IDE loading separately.
- No Roslyn/Workspaces framework DLLs are redistributed into the Unity project. IDE hosts provide them.
- Version source/package 0.2.0; preserve existing metadata GUIDs and add metadata for new imported assets.

## Source transformation contract

- Export a public OrderingCodeFixProvider for DGUA001/DGUA002. Offer English actions
  "Group serialized fields" and "Order Unity methods" with stable diagnostic-specific equivalence keys.
- A single action fixes the nearest type declaration containing the selected diagnostic. Do not move members
  across partial declarations, files or enclosing/nested types. Fix All supports Document, Project and Solution.
- Reuse AnalyzerSettings.Load(...).ForTree(...) and semantic predicates. Additional files and per-tree
  EditorConfig precedence must remain identical to diagnostics; cancellation propagates.
- DGUA002: reorder direct method slots only, preserving every non-method member's slot. When placement is true,
  stably partition recognized Unity methods before ordinary methods. Sort ranked callbacks within ranked slots;
  retain unranked callback order and ordinary method order. When placement is false, only ranked callback slots
  are sorted, leaving helpers/unranked callbacks in their original slots.
- DGUA001: stably partition direct instance field declaration slots by marker and configured first/last setting.
  Keep static/const/non-field slots untouched and multi-variable declarations intact. Reuse semantic markers.
- Move syntax nodes with attributes, XML documentation, comments and trivia intact. Do not normalize the whole
  document or reformat unrelated source. Preserve newline style, non-target members and method bodies.
- Decline a type containing directives or syntax errors. Do not move across #if/#region/#pragma/#nullable.
- DGUA001 also declines structs and classes explicitly marked StructLayout. Preserve the relative order of
  ALL existing instance initializer expressions (fields, auto-properties and field-like events); decline the
  whole field action if the permutation would change it. No guesses about initializer purity.
- Fix All processes each affected declaration once per document and preserves nested changes without overlapping
  text edits. Use diagnostics supplied by the host so suppression/scope is respected. Unsafe declarations remain
  unchanged while safe declarations can be fixed. A second invocation makes no further changes.
- Do not register a no-op action. Do not fix unrelated diagnostics or infer a callback from its name alone.

## Ownership

| Slice | Owner | Owned files | State |
| --- | --- | --- | --- |
| Settings sharing and project scaffolding | Coordinator | analyzer helper access/AssemblyInfo, solution, root props/docs | Shared predicates and solution/version ready; docs updated |
| Code fixes and Fix All | Code-fix executor | src/DraasGames.Unity.Analyzers.CodeFixes/** | Implemented and reviewed; frozen for verification |
| IDE integration and packaging | Packaging executor | package/Editor/** and .meta, IDE DLL .meta, scripts/Build-Package.ps1, docs/ide-integration.md | Implemented; frozen for integration tests |
| Tests and verification | Separate verifier | tests/** and artifacts/0.2.0/** | Complete: strict Release build clean, 39/39 passed, package metadata/content and built/packaged/extracted DLL hashes verified |

## New tests — approved 2026-10-05

The user explicitly approved adding the 16 methods below and running all 39 methods on 2026-10-05.

All rows: ADD and RUN. Namespace/class: DraasGames.Unity.Analyzers.Tests.OrderingCodeFixTests for rows 1–14;
IdeIntegrationTests for rows 15–16. The tests exercise public CodeFixProvider/CodeAction/FixAllProvider APIs
and the actual project-generation hook with narrow Unity API stubs. Expectations are handwritten document
transformations/member order, preserved content, compilation and diagnostic results, not implementation greps.
The public hook tests do not prove execution inside a real Unity Editor/Rider process.

| # | Method | Scenario and expected result |
| --- | --- | --- |
| 1 | CallbackFix_GroupsAndOrdersMethods | Helpers before callbacks and callback inversions are fixed together; extended messages, Unity overrides/interfaces participate; ordinary/unranked order stays stable; resulting code compiles and DGUA002 clears. |
| 2 | CallbackFix_RespectsConfiguration | Custom/subset order and placement=false follow additional files and EditorConfig override; helpers/unranked methods retain slots when placement is disabled. |
| 3 | CallbackFix_PreservesMemberContent | Attributes, XML/comments, method bodies, non-method slots and CRLF/LF remain intact after reordering. |
| 4 | FieldFix_GroupsAllSerializedMarkers | Unity/Odin markers and aliases group stably; foreign attributes, static/const and multi-variable fields retain the documented behavior; DGUA001 clears. |
| 5 | FieldFix_RespectsConfiguration | First/last and EditorConfig-over-additional-file precedence produce the independently expected field order. |
| 6 | FieldFix_PreservesInitializerOrder | Moves without initializer reordering work; changes to the execution order of field/property/event initializers offer no fix. |
| 7 | FieldFix_DeclinesLayoutSensitiveTypes | Structs and explicit/sequential StructLayout classes offer no field fix; ordinary classes remain eligible. |
| 8 | CodeFix_DirectivesSuppressAction | Each provider declines types containing #if/#region/#pragma/#nullable; corresponding directive-free violations have an action. |
| 9 | CodeFix_PartialAndNestedTypesStayLocal | A single action changes only its targeted declaration; nested/outer/other partial declarations and files remain unchanged. |
| 10 | CodeFix_CancellationDoesNotReturnChanges | Cancellation during registration/application/Fix All propagates without returning partially edited documents. |
| 11 | FixAll_DocumentFixesEveryAffectedType | Multiple diagnostics per type plus nested and sibling types resolve without overlapping edits; both diagnostic-specific actions work. |
| 12 | FixAll_ProjectRespectsScope | Fixes every eligible document in the selected project, leaving another project and suppressed violations unchanged. |
| 13 | FixAll_SolutionUsesEachDocumentsSettings | Across projects/documents, each file's effective settings and the selected diagnostic ID govern the result. |
| 14 | FixAll_SkipsUnsafeTypesAndIsIdempotent | Safe types change, unsafe types stay byte-for-byte intact; second application yields no further changes. |
| 15 | IdeIntegration_AddsCodeFixOnlyToAnalyzedProjects | Hook adds IDE Analyzer reference only when the base analyzer and IDE DLL are present; other projects/missing package files remain unchanged. |
| 16 | IdeIntegration_PreservesProjectAndAvoidsDuplicates | XML namespace, existing items and escaped/Unicode paths survive; repeat calls do not duplicate entries; malformed XML is returned unchanged. |

Regression: RUN the 23 already-approved OrderingAnalyzerTests methods listed in docs/specification.md unchanged.
Approved total: 39 test methods. Preserve the
current 0.1.2 baseline for an observable pre-feature no-code-fix comparison, without adding fake production seams.

## Acceptance

Release solution build, all approved diagnostic/code-fix/integration tests, verified package metadata and matching
built/packaged DLLs, English user guidance and explicit real-Unity/Rider validation boundary. New tests awaiting
approval are a remaining acceptance gate, not a reason to stop authorized production implementation.
