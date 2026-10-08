# PowerDesigner 16.7.4 visual baseline

The source is `tests/Bing.Pdm.Tests/Fixtures/native-visual.pdm`, a synthetic model
created in local PowerDesigner 16.7.4.6866. It has two tables, columns, primary
key markers, and one reference. `native-visual.png` is the original color export
from the model's default physical diagram (`Edit > Export in Color` enabled).

- Windows system DPI: 96 (100%); Chromium headless is used for the SVG rasterization.
- Native image: 183 x 148 PNG. PowerDesigner default diagram scale: 100%.
- Fonts: Arial, 8 pt as stored in the PDM's `FontList`.
- `native-visual-PhysicalDiagram_1.svg` is the project's `PowerDesigner` mode output from the same PDM.
- `native-geometry.json` records manually measured table bounds in the original PNG.
- Its `lines` entries record connection endpoints measured from the original PNG;
  `compare-native.cjs` reports their coordinate error separately from rectangles.
- `tests/export-native-baseline.ps1` uses the installed PowerDesigner 16.7.4.6866
  COM interface to export a selected root-model physical diagram. It closes the
  model without saving and refuses to overwrite an existing PNG. Re-exporting
  this fixture produces an image with the same file hash as `native-visual.png`:

  ```powershell
  & tests/export-native-baseline.ps1 -ModelPath tests/Bing.Pdm.Tests/Fixtures/native-visual.pdm -DiagramCode PhysicalDiagram_1 -OutputPath new-native-visual.png
  ```

- `node tests/browser-smoke/compare-native.cjs` recreates the four PNG artifacts and `metrics.json`.
- `native-annotations.pdm` adds a Chinese RTF Note to the same synthetic model.
  PowerDesigner 16.7.4.6866 opened this file and exported
  `native-annotations.png`. Its independent comparison artifacts are in
  `native-annotations/`; regenerate them with:

  ```powershell
  dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/native-annotations.pdm docs/visual-baseline/native-annotations svg en --diagram-style powerdesigner
  node tests/browser-smoke/compare-native.cjs native-annotations
  ```
- `native-typography.pdm` is the native-saved version of the synthetic model
  with a 12 pt italic Times New Roman title and 11 pt Times New Roman columns.
  `native-typography.png` was exported from that saved PDM with PowerDesigner
  16.7.4.6866. Its comparison artifacts are in `native-typography/`; `native-typography/native-original.svg` is retained for Times New Roman text-column coordinate comparison and field-name width layout calibration:

  ```powershell
  dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/native-typography.pdm docs/visual-baseline/native-typography svg en --diagram-style powerdesigner
  node tests/browser-smoke/compare-native.cjs native-typography
  ```
- `native-shapes.pdm` is another PowerDesigner-saved synthetic model. Its
  architecture area contains an ellipse, text and a polyline. Re-exporting the
  saved model produced a PNG with the same file hash as the first valid native
  export. The comparison artifacts are in `native-shapes/`:

  ```powershell
  dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/native-shapes.pdm docs/visual-baseline/native-shapes svg en --diagram-style powerdesigner
  node tests/browser-smoke/compare-native.cjs native-shapes
  ```
- `native-dash.pdm` was saved after setting the reference symbol's line style
  to `Dash` through PowerDesigner automation. The saved XML contains
  `DashStyle=2`; its native SVG uses a 16:8 dash pattern:

  ```powershell
  dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/native-dash.pdm docs/visual-baseline/native-dash svg en --diagram-style powerdesigner
  node tests/browser-smoke/compare-native.cjs native-dash
  ```
- `native-dot.pdm` follows the same native workflow with the `Dot` style,
  stored as `DashStyle=3`. Its native SVG uses a 3:3 dot pattern:

  ```powershell
  dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/native-dot.pdm docs/visual-baseline/native-dot svg en --diagram-style powerdesigner
  node tests/browser-smoke/compare-native.cjs native-dot
  ```
- `native-dashdot.pdm` and `native-dashdotdot.pdm` are copies of the synthetic
  Dash fixture with only `DashStyle` changed to `4` and `5`. PowerDesigner
  16.7.4.6866 opened both candidates and exported their original PNG and SVG.
  The native SVG dash patterns are `8,4,2,4` and `8,4,2,4,2,4` respectively.
  Regenerate either comparison by substituting its fixture name below:

  ```powershell
  dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/native-dashdot.pdm docs/visual-baseline/native-dashdot svg en --diagram-style powerdesigner
  node tests/browser-smoke/compare-native.cjs native-dashdot
  ```
- `native-keys.pdm` copies the synthetic table sample and enables
  `Table.Keys=Yes`. PowerDesigner 16.7.4.6866 exported its color PNG and
  original SVG. The current renderer draws the primary-key pane, divider,
  label, marker and icon, and extends the table and connection geometry:

  ```powershell
  dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/native-keys.pdm docs/visual-baseline/native-keys svg en --diagram-style powerdesigner
  node tests/browser-smoke/compare-native.cjs native-keys
  ```
- `native-indexes.pdm` enables the index pane and contains one index on the
  Orders table. PowerDesigner 16.7.4.6866 exported the original PNG and SVG;
  the project SVG adds the pane to both table symbols and renders the index
  label and icon. `native-indexes-regular.pdm` changes only the index's
  uniqueness; its native SVG uses the same pane icon. The single-index
  comparison is regenerated with:

  ```powershell
  dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/native-indexes.pdm docs/visual-baseline/native-indexes svg en --diagram-style powerdesigner
  node tests/browser-smoke/compare-native.cjs native-indexes
  ```
- `native-multi-indexes.pdm` adds a second index to Orders. Its original SVG
  shows a 13 px second row; the renderer expands the table around the original
  rectangle center and keeps the other table at its single-row height.
  `native-alternate-key.pdm` uses the same layout with one alternate key and
  adds `<ak>` markers to the key row and column:

  ```powershell
  dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/native-multi-indexes.pdm docs/visual-baseline/native-multi-indexes svg en --diagram-style powerdesigner
  node tests/browser-smoke/compare-native.cjs native-multi-indexes
  dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/native-alternate-key.pdm docs/visual-baseline/native-alternate-key svg en --diagram-style powerdesigner
  node tests/browser-smoke/compare-native.cjs native-alternate-key
  ```
- `native-keys-indexes.pdm` enables both panes. Its native PNG and SVG show
  the key row before the index row and an 81 px table height. The separate
  comparison is regenerated with:

  ```powershell
  dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/native-keys-indexes.pdm docs/visual-baseline/native-keys-indexes svg en --diagram-style powerdesigner
  node tests/browser-smoke/compare-native.cjs native-keys-indexes
  ```

The historical candidate recorded pixel mean absolute error is 18.123504652193176/255 and the fraction of pixels
with a mean RGB difference above 10 is 0.192512184315463. These metrics are descriptive
only. The upper table spans the full native image width; its bounds were
confirmed by scanning source pixels. Its reference endpoints differ by 0.40 px
per coordinate on average. The source diagram disables table
shadows and uses a diagonal gradient. PowerDesigner mode uses the native white
canvas. The renderer has not been manually
accepted as pixel equivalent to PowerDesigner. The annotation case has a
16.7049106192807/255 mean absolute error and a 0.17852202596297084 changed-pixel fraction. Its Note
rectangle differs by about 0.69 px on average. The Chinese text's black-pixel
bounds still require manual review after font calibration. The typography case
has a 16.04111111111111/255 mean absolute error and a 0.18423976608187134 changed-pixel fraction.
Its two table rectangles differ by 0.18 and 0.59 px on average. The nested-shape
case has a 12.615604855862047/255 mean absolute error and a 0.12748273943915447 changed-pixel fraction;
its area and ellipse bounds differ by 0.33 and 0.18 px on average. Its reference
endpoints differ by 0.43 px per coordinate on average. The area
icon is approximated in the project SVG. The dashed reference case has a
18.13982425047999/255 mean absolute error and a 0.19236449564318417 changed-pixel fraction. The dotted
reference case has a 18.150937823068972/255 mean absolute error and a 0.19232757347511445 changed-pixel
fraction. Dash-dot has a 18.205028799291096/255 mean absolute error and a 0.1924752621473933 changed-pixel
fraction; dash-dot-dot has 18.14946093634618/255 and 0.1924014178112539. Additional PowerDesigner line
styles remain unverified.

Columns that are both primary-key and foreign-key members are now marked as
`<pk,fk>` in the rendered diagram.
The key-pane case has a 21.911011966521407/255 mean absolute pixel error and a
0.23922667220031818 changed-pixel fraction. Its two table bounds differ by
0.06 and 0.14 px on average; the reference endpoints differ by 0.38 px.
The index-pane case has a 20.829978557100368/255 mean absolute pixel error
and a 0.22283322957736737 changed-pixel fraction. Its two table bounds differ
by 0.06 and 0.14 px on average; the reference endpoints differ by 0.38 px.
The double-index and alternate-key cases have 22.45/255 and 24.06/255 mean
absolute pixel error respectively. Their upper-table bounds differ by 0.02 px,
lower-table bounds by 0.30 px, and reference endpoints by 0.54 px on average.
The combined key/index case has a 23.82/255 pixel error, 0.04/0.36 px
upper/lower table bounds errors, and a 0.59 px reference endpoint error.
Trigger panes and custom column filters were unsupported in that historical candidate.
Current support and evidence are described below.

## Candidate evidence (2026-10-05)

The 13 native fixtures were re-evaluated after the comparison/JSON and RTF style
changes. Fresh artifacts are stored in the local ignored directory
`artifacts/native-compatibility/20261005-63428cb3cd364fdfadef700ed9dfaff9`.
Original PDM/PNG/SVG sources and previous comparison artifacts remain intact.
The older key/index pixel statistics above refer to their previous candidate;
use this run's `summary.json` and per-fixture metrics for the current candidate.

`compare-native.cjs` accepts a second argument naming an independent candidate
output directory containing the freshly exported SVG. It reads reference geometry
from this baseline directory, refuses to overwrite an existing comparison in the
explicit output directory, and records input/reference/SVG/assembly/harness
identities, browser version and runtime in `evidence.json`:

```powershell
$candidateDir = Join-Path 'artifacts/native-review' ([guid]::NewGuid().ToString('N'))
dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/native-annotations.pdm $candidateDir svg en --diagram-style powerdesigner
node tests/browser-smoke/compare-native.cjs native-annotations $candidateDir
```

No pixel threshold was added. All evidence remains `PENDING_HUMAN_REVIEW`.
`rich-text-styles.pdm` verifies fonts, scoped foreground colors, underline and
per-symbol shadows through unit/browser tests; it is not an original PowerDesigner
visual baseline and must not be used as one.


## Layout, triggers and current candidate (2026-10-05)

The current run covers 15 native fixtures and supersedes the older candidate
statistics above. It is stored in
`artifacts/native-compatibility/30e8ce6248f24e4f81f286283f36f7a3/summary.json`.
Each evidence file also includes the CLI assembly hash because the Windows GDI
measurement provider belongs to the CLI. The core accepts an explicit measurement
callback; without one it reports approximate metrics. The 8pt conversion was
calibrated against 10px text in the native SVG.

Two new public originals were exported with PowerDesigner 16.7.4.6866:

- `native-triggers` contains an after-insert trigger and enabled trigger panes.
  The native `Time`, `Event`, `Text` attributes are preserved; its original SVG
  supplies the pane geometry and icon. Current pixel MAE is 19.17/255.
- `native-paragraphs` contains centered/left/right RTF paragraphs, bold text,
  foreground color and CP936 Chinese. It verifies measured word wrapping and
  clipping. Its original SVG embeds the native RTF bitmap in the accompanying
  `_svg_Files` directory. Geometry metrics cover its tables and reference;
  the note is included in pixel statistics but has no separate rectangle entry.
  Current pixel MAE is 17.01/255.

The basic table case is now 16.61/255. These values remain descriptive,
`PENDING_HUMAN_REVIEW`; no pixel threshold was introduced. Known visual mismatches
include native RTF white text backgrounds and visible row counts, text rasterization,
some spacing, and the approximated area icon. The browser verifies meaningful
content, desktop/mobile views, no external requests, links and zoom. It does not
replace manual native acceptance.

Builtin all-column/primary-key/key-column filters preserve model column order.
Custom expressions can be handled by the explicit `ColumnFilter` callback;
uninterpreted expressions retain a style diagnostic rather than silently executing
PowerDesigner code. Further unsupported styles also remain diagnostic.
