# Bing.Pdm

Bing.Pdm reads PowerDesigner XML PDM files without PowerDesigner. One parsed
model feeds JSON export, Markdown and offline HTML data dictionaries, physical
diagram rendering, and code generators.

## Run

The core library targets `netstandard2.0`; the CLI and tests target `net8.0`.

```powershell
dotnet run --project samples/Bing.Pdm.Tool -- export model.pdm output json,md,html,svg,xlsx,docx zh
dotnet run --project samples/Bing.Pdm.Tool -- generate model.pdm entities Demo.Entities
dotnet run --project samples/Bing.Pdm.Tool -- generate model.pdm entities Demo.Entities --fallback-type object
dotnet test Bing.Pdm.sln
```

Run the offline HTML browser smoke test with Playwright from
`tests/browser-smoke` (`npm ci`, `npx playwright install chromium`, then
`npm test`). Set `PDM_BROWSER_EXECUTABLE` to use an installed Chromium-based
browser such as Edge instead of Playwright's downloaded Chromium. The test
exports the repository fixture, opens it as a local file, and checks search,
diagram links and zoom, external requests, browser errors, and a narrow viewport.

`export` accepts `json`, `md`, `html`, `svg`, `xlsx`, and `docx`; omitting the
format list writes JSON, Markdown, and HTML. The last argument selects `en` or
`zh`. SVG exports one standalone file per physical diagram. The HTML dictionary
embeds its CSS, JavaScript, and SVG, with search, a table/view/diagram contents
list, diagram zoom, and links between diagram symbols and dictionary entries.
The Excel workbook has sheets for tables, columns, keys and indexes,
references, views, view columns, and diagnostics. The Word dictionary includes
the same schema details in a landscape document with repeated table headers.
Both files are generated as OOXML without external services or dependencies.

`generate` writes a C# entity per table. The example type map recognizes common
SQL types and scopes vendor-specific types by the PDM DBMS. Add repeatable
`--map` options as `--map MSSQLSRV:geography=My.App.Geography` to override a
type for one DBMS; use `*` instead of the DBMS for a global override. Mappings
can also be registered through the public `CSharpTypeMapper` class in the CLI
assembly. Generation scans all columns before writing files. By default, all
missing and unsupported types are reported together and no output directory is
created. `--fallback-type object` explicitly substitutes `object` for each
unresolved type and prints a `TYPE_FALLBACK [Table.Column]` warning. It never
guesses misspelled SQL types. Mapping precedence is DBMS-specific exact/base,
DBMS built-ins, global exact/base, then explicit fallback. The CLI assembly
also exposes `CSharpTypeMapper.RegisterFallback` and `Resolve`, and
`EntityGenerator.GenerateWithDiagnostics`.
Programmatic consumers can pass an `ICSharpEntityTemplate` to `EntityGenerator`
to control source layout; its context contains collision-safe names, mapped CLR
types, and the original table/column objects.

The standalone SmartCode 2.2 adapter in `samples/Bing.PdmGenerateDemo` targets
.NET 8 and accepts `--pdm` and `--output` (or the matching settings in
`appsettings.json`). It maps the same `PdmInfo` tables and columns into
SmartCode's table model. Its package graph still contains legacy dependencies,
including packages reported vulnerable by NuGet, so use the main .NET 8 CLI as
the supported generator path. The adapter stays outside the solution as a
compatibility example.

## Library API

```csharp
using System.Collections.Generic;
using Bing.Pdm;
using Bing.Pdm.Reader;

var model = new PdmReader().ReadFromFile("model.pdm");
var labels = new DictionaryPdmExportLabelProvider(new Dictionary<string, string>
{
    ["Tables"] = "Entities",
    ["SheetColumns"] = "Properties"
});
var exporter = new PdmExporter(PdmExportLanguage.English, labels);
using (var html = File.CreateText("dictionary.html"))
    exporter.Write(model, PdmExportFormat.Html, html);

using (var writer = File.CreateText("dictionary.md"))
    new PdmExporter().Write(model, PdmExportFormat.Markdown, writer);

using (var stream = File.Create("dictionary.xlsx"))
    new PdmOfficeExporter(PdmExportLanguage.Chinese).WriteExcel(model, stream);
```

Custom export labels override the built-in English or Chinese labels. Missing
or empty custom values fall back to the selected language. To replace the
Markdown or HTML layout, implement `IPdmExportTemplate`; the exporter passes
the parsed `PdmInfo`, the same resolved label provider, and the output writer.
JSON and SVG keep their stable built-in serializers.

`IPdmReader.Read(Stream)` leaves the supplied stream open. `PdmInfo` keeps
root-level objects and recursive packages separately. `AllTables`, `AllViews`,
`AllReferences`, `AllShortcuts`, and `AllPhysicalDiagrams` provide flattened views. Original
PDM IDs are retained, and relationships use IDs rather than object pointers;
JSON therefore has no reference cycles. `Lookup` provides ID-based lookups for
tables, packages, views, references, columns, keys, owners, diagrams, and
symbols and shortcuts after parsing. A shortcut retains its original PDM ID,
ObjectID, TargetID and resolved target ID. Relations and table diagram symbols
retain raw Shortcut references separately from normalized real table IDs.

The reader handles model metadata, packages, tables, columns, keys, indexes,
views, references and joins, table shortcuts, `IndexColumns/IndexColumn`, and
physical diagram symbols. It
accepts the `Columns`/`ColumnInfos`, `Keys`/`KeyInfos`, and
`Indexes`/`IndexeInfos` collection names seen in PDM variants. It rejects XML
DTDs and external entities. Missing model nodes and malformed XML fail; missing
object references produce `UNRESOLVED_REF` entries in `PdmInfo.Diagnostics` and
missing required references produce `MISSING_REF`. Duplicate IDs,
malformed diagram coordinates, and incomplete composite foreign-key joins are
reported as `DUPLICATE_ID`, `MALFORMED_GEOMETRY`, and
`FOREIGN_KEY_KEY_MISMATCH`. Diagnostics are included in JSON, Markdown, HTML,
Excel, and Word exports. Consumers should inspect them before relying on a
generated schema. Unresolved active shortcuts produce
`UNRESOLVED_SHORTCUT_TARGET`; ambiguous target ObjectIDs produce
`AMBIGUOUS_SHORTCUT_TARGET`. Unknown visible symbol types produce
`UNSUPPORTED_DIAGRAM_SYMBOL`.

Diagrams render tables, packages, notes, text, ellipses, polylines, predefined
shapes, architecture areas, note links and extended dependencies. Nested
`SubSymbols` are traversed recursively. Rectangles, connector paths and basic
PowerDesigner BGR `COLORREF` values are preserved; visual effects and font
fidelity are outside the supported scope. RTF note content becomes escaped
plain text, including CP936 Chinese. `TargetModel`, `Replication` and
`SubReplication` are not part of the unified model. Shortcuts resolve only to
objects in the current root model.

`IPdmExporter.Write` accepts JSON, Markdown, HTML, or a single SVG diagram. `WriteDiagram` writes a
selected standalone SVG. `PdmOfficeExporter.WriteExcel` and `WriteWord` write
to caller-owned streams and leave those streams open.

Physical diagrams preserve symbol rectangles and connector bend points. The
HTML SVG flips the PDM vertical axis into browser coordinates and uses a
consistent display style; it does not reproduce PowerDesigner fonts, colors,
shadows, or every symbol type. The supported format is the XML PDM family
represented by `templates/pdm_template.xml` and the test fixtures.
