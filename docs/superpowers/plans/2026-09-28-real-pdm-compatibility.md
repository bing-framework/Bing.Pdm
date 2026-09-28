# Real PDM compatibility

## Goal

Read the four supplied PowerDesigner XML PDM files through the existing
`netstandard2.0` library, and keep JSON, Markdown, HTML/SVG and C# generation
on one parsed model. The business PDM files remain outside the repository.

## Implementation

1. Add recursive Shortcut and IndexColumn model data. Resolve active table
   shortcuts by TargetID to a unique real table ObjectID; preserve raw refs.
2. Parse supported note, shape, area and connector symbols recursively, with
   basic COLORREF colors and CP936 RTF plain text. Diagnose unknown visible
   symbols and unresolved or ambiguous shortcuts.
3. Preflight all entity column types before writing files. Keep strict mode by
   default; permit explicit `--fallback-type` and retain `--map` precedence.
4. Add synthetic fixtures and tests, then validate the four original files,
   generated entities, solution tests and offline HTML browser behavior.

## Support boundary

`TargetModel`, `Replication` and `SubReplication` are deferred. Shortcut targets
must exist in the current root model. Diagram rendering preserves layout,
connections, annotation text and basic color, without pixel-level styling.
