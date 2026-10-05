# Assembly scope

Unity provides two controls that apply to both this package and StyleCop Analyzers:

- Analyzer placement relative to an assembly definition controls which assemblies receive the analyzer.
- Rule sets control which diagnostics are enabled and their severity for each assembly.

These are different controls. Disabling diagnostics with a ruleset does not mean removing the analyzer DLL from compilation.

## Rule sets per assembly

| Location | Scope |
| --- | --- |
| `Assets/Default.ruleset` | Default diagnostic policy for predefined and custom assemblies. |
| `Assets/Assembly-CSharp.ruleset` | Override for the predefined Assembly-CSharp assembly. |
| `Assets/Assembly-CSharp-Editor.ruleset` | Override for the predefined Assembly-CSharp-Editor assembly. |
| A `.ruleset` next to a custom `.asmdef` | Override for that custom assembly. Its ruleset filename need not match the assembly name. |

For example:

```text
Assets/
  Default.ruleset
  Game/
    Runtime/
      Game.Runtime.asmdef
      Game.Runtime.ruleset
    Editor/
      Game.Editor.asmdef
      Game.Editor.ruleset
```

To enable selected rules only in your own assemblies, disable those rules in the default policy and enable
them in the ruleset beside each target `.asmdef`. Merely adding a local ruleset does not disable diagnostics elsewhere.

Example entries for `Assets/Default.ruleset`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RuleSet Name="Default style policy" ToolsVersion="15.0">
  <Rules AnalyzerId="DraasGames.Unity.Analyzers" RuleNamespace="DraasGames.Unity.Analyzers">
    <Rule Id="DGUA001" Action="None" />
    <Rule Id="DGUA002" Action="None" />
  </Rules>
  <Rules AnalyzerId="StyleCop.Analyzers" RuleNamespace="StyleCop.Analyzers">
    <Rule Id="SA1202" Action="None" />
  </Rules>
</RuleSet>
```

Example entries for `Game.Runtime.ruleset` next to `Game.Runtime.asmdef`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RuleSet Name="Game runtime style" ToolsVersion="15.0">
  <Rules AnalyzerId="DraasGames.Unity.Analyzers" RuleNamespace="DraasGames.Unity.Analyzers">
    <Rule Id="DGUA001" Action="Warning" />
    <Rule Id="DGUA002" Action="Warning" />
  </Rules>
  <Rules AnalyzerId="StyleCop.Analyzers" RuleNamespace="StyleCop.Analyzers">
    <Rule Id="SA1202" Action="Warning" />
  </Rules>
</RuleSet>
```

This example changes **only SA1202**, not all StyleCop rules. Repeat the entries for the StyleCop rules in your
actual policy, including every active rule you want disabled elsewhere. Omitted rules retain their applicable/default
severity. Rule IDs are explicit; `SA*` is not a supported wildcard. Avoid globally disabling unrelated diagnostics.
Merge these entries with existing rulesets instead of overwriting them. SA1202 is illustrative; it can conflict
with the Unity-method/serialized-field placement convention, so keep it disabled if that is your chosen policy.

## Restricting analyzer placement

An analyzer DLL beneath an `.asmdef` is scoped to that assembly and assemblies that reference it.
This is not an exact whitelist of arbitrary assembly names. Consider the reference graph before moving analyzers
under a shared assembly. The DLL in this package is currently outside an `.asmdef`.

Keep Unity `.meta` files when distributing analyzer assets. Do not edit immutable package-cache files to change scope;
configure your project's rulesets or change the source package deliberately.

## IDE and Unity

EditorConfig can control diagnostics for file paths when the host supplies those settings to Roslyn.
Folder-based EditorConfig settings do not represent an assembly-name filter. For consistent assembly policies,
verify the selected ruleset in Unity compilation and the generated project used by Rider. This package currently
has no custom assembly-name include/exclude setting.

The XML above is a configuration example, not a claim of validation in a consumer Unity project.

Source: [Unity: analyzer scope and rule set files](https://docs.unity3d.com/6000.0/Documentation/Manual/analyzer-scope-and-diagnostics.html).
