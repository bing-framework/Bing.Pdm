# PDM C# Type Mapper Implementation Plan

> **For agentic workers:** Execute this plan inline in the current task. The user has authorized implementation.

**Goal:** Make the sample C# entity generator extensible and improve built-in mappings for common SQL Server, PostgreSQL, MySQL, and Oracle types.

**Architecture:** Move type selection out of the CLI entry point into a public `CSharpTypeMapper` in the tool assembly. Normalize vendor names and database type declarations, apply vendor-specific built-ins, then apply registered exact vendor/type overrides before nullable handling.

**Tech Stack:** C# 12, .NET 8, xUnit.

**Spec:** User-requested continuation of the incomplete-capabilities audit, specifically additional and customizable database type mappings.

## Global Constraints

- The core parsed model remains database-language neutral.
- Type mapping stays in the generator/tool side.
- Unsupported database types continue to fail with column and DBMS context.

---

### Task 1: Extract and extend C# type mapping

**Files:**
- Create: `samples/Bing.Pdm.Tool/CSharpTypeMapper.cs`
- Modify: `samples/Bing.Pdm.Tool/Program.cs`
- Modify: `tests/Bing.Pdm.Tests/Bing.Pdm.Tests.csproj`
- Test: `tests/Bing.Pdm.Tests/PdmReaderTests.cs`
- Modify: `README.md`

**Interfaces:**
- Produce `CSharpTypeMapper.GetCSharpType(ColumnInfo column, string dbmsCode, string dbmsName)`.
- Produce `CSharpTypeMapper.RegisterMapping(string databaseType, string clrType, string dbms = null)` for exact custom overrides.
- `EntityGenerator` consumes `CSharpTypeMapper`; unsupported types retain current fail-fast behavior.

- [x] Add direct tests for PostgreSQL aliases/time-zone types, MySQL unsigned and boolean types, Oracle date/number/binary types, and a custom vendor mapping.
- [x] Run focused tests and confirm the new public mapper API and missing aliases fail.
- [x] Implement normalized built-in mappings and exact vendor/type registration; keep nullable conversion after type selection.
- [x] Run focused tests and the CLI generation/compile integration test; document mapping overrides.

### Task 2: Self-review

- [x] Verify SQL Server `timestamp` remains `byte[]` and PostgreSQL `timestamp` remains `DateTime`/`DateTimeOffset` as appropriate.
- [x] Verify invalid custom registration fails deterministically and generated C# remains compilable.
