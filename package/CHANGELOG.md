# Changelog

## 0.2.0 — 2026-10-05

- Add IDE quick-fixes for serialized field grouping and Unity method ordering.
- Add diagnostic-specific Fix All support for documents, projects and solutions.
- Preserve comments, attributes and non-target members; skip unsafe directives, layout-sensitive fields
  and changes to instance initializer execution order.
- Bundle a separate IDE code-fix assembly and Editor-only IDE project-generation integration.

## 0.1.2 — 2026-10-05

- Expand DGUA002 recognition to Unity runtime and Editor messages, semantic Unity API overrides, and Unity interface implementations.
- Group every recognized Unity method before ordinary methods, even when omitted from the relative callback-order list.
- Keep the existing ten lifecycle names as the configurable relative-order subset.
- Document per-assembly ruleset configuration for DraasGames analyzers and StyleCop.

## 0.1.1 — 2026-10-05

- DGUA002 now requires checked Unity callbacks before ordinary methods by default, including a lone Awake after a binding method.
- Add draas_unity_callbacks_before_other_methods; set false to retain relative callback ordering only.
- Keep recognized callbacks omitted from the configured list excluded from both ordering checks.
- Include Unity metadata for README.md, CHANGELOG.md, and package.json so immutable package imports can recognize them.
- Check metadata coverage before creating a UPM archive; skip Unity-ignored folders such as Documentation~.

## 0.1.0 — 2026-10-05

- DGUA001: semantic Unity/Odin attribute-based instance field grouping.
- DGUA002: configurable Unity callback declaration order.
- Per-project EditorConfig and Unity additional-file settings.
- Prebuilt UPM analyzer DLL and reproducible local package build script.
