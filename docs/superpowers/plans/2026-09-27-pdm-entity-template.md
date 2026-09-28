# PDM Entity Template Implementation Plan

> **For agentic workers:** Execute this plan inline in the current task. The user has authorized implementation.

**Goal:** Allow code generators to replace generated entity source layout while reusing PDM naming and type mapping.

**Architecture:** Expose an entity-template contract and immutable render context from the .NET 8 tool assembly. `EntityGenerator` continues to resolve unique entity/property names and database types, then delegates source rendering; its current output becomes the built-in default template.

**Tech Stack:** C# 12, .NET 8, xUnit.

**Spec:** User-requested continuation of incomplete generator capabilities, specifically pluggable code-generation templates.

## Global Constraints

- The core parsed model remains database-language neutral.
- Existing CLI generation behavior remains the default.
- The generated source path and identifier collision handling remain owned by `EntityGenerator`.

---

### Task 1: Add the entity template contract

**Files:**
- Create: `samples/Bing.Pdm.Tool/CSharpEntityTemplate.cs`
- Modify: `samples/Bing.Pdm.Tool/Program.cs`
- Test: `tests/Bing.Pdm.Tests/PdmReaderTests.cs`
- Modify: `README.md`

**Interfaces:**
- Produce `ICSharpEntityTemplate.Render(CSharpEntityTemplateContext context)`.
- Produce a context containing namespace, entity name, source `TableInfo`, and the resolved property names/types/source columns.
- Add an optional template parameter to `EntityGenerator.Generate`; null selects the built-in current layout.

- [x] Add a custom-template test and assert it receives mapped, collision-safe entity/property identifiers and emits the chosen source.
- [x] Run the focused test and confirm the template API was not yet available.
- [x] Extract current source formatting into the built-in template and route generation through the selected template.
- [x] Run the focused test and CLI entity compile test; document the programmatic extension point.

### Task 2: Self-review

- [x] Confirm null template preserves existing output and the CLI integration test still compiles generated entities.
- [x] Confirm custom template receives the original table/column model and mapped CLR type without mutating `PdmInfo`.
