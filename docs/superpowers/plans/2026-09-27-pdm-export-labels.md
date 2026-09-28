# PDM Export Label Provider Implementation Plan

> **For agentic workers:** Execute this plan inline in the current task. The user has authorized implementation.

**Goal:** Let callers override export labels without changing the parser or forking the exporters.

**Architecture:** Add a public label-provider contract and a dictionary-backed implementation in the core library. Existing constructors and built-in English/Chinese labels remain the fallback; both text and Office exporters receive the same optional provider. Markdown and HTML can each be replaced through a format-specific template that receives the model and resolved labels.

**Tech Stack:** C# 7.1, .NET Standard 2.0, xUnit on .NET 8.

**Spec:** User-requested continuation of the incomplete-capabilities audit, specifically pluggable localization resources.

## Global Constraints

- The core library remains `netstandard2.0`.
- Existing public constructors and built-in labels remain compatible.
- Exporters continue writing to caller-owned writers and streams.

---

### Task 1: Add injectable labels to exporters

**Files:**
- Modify: `src/Bing.Pdm/PdmExporter.cs`
- Modify: `src/Bing.Pdm/PdmOfficeExporter.cs`
- Test: `tests/Bing.Pdm.Tests/PdmReaderTests.cs`
- Modify: `README.md`

**Interfaces:**
- Produce `IPdmExportLabelProvider.GetLabel(string key)`.
- Produce `DictionaryPdmExportLabelProvider(IDictionary<string, string> labels)`.
- Produce `IPdmExportTemplate.Format` and `Write(PdmInfo model, IPdmExportLabelProvider labels, TextWriter writer)` for Markdown or HTML.
- Add constructor overloads to `PdmExporter` and `PdmOfficeExporter` while retaining current constructors.

- [x] Add a test using a custom `Tables` label and a custom Office sheet/column label; assert the HTML, workbook sheet name, and header use those values.
- [x] Run the focused test and confirm it fails because current exporters cannot accept a provider.
- [x] Implement provider fallback: a missing or empty custom value delegates to the existing English/Chinese label.
- [x] Run the focused test, then the full test project; document the API and fallback behavior.

### Task 2: Self-review

- [x] Confirm existing constructors produce unchanged labels for representative English and Chinese exports.
- [x] Confirm custom labels are XML/HTML encoded by the existing output writers.
- [x] Confirm a custom Markdown or HTML template receives the parsed model and fallback-resolved labels.
