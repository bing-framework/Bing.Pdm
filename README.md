# Bing.Pdm

Bing.Pdm 可在不安装 PowerDesigner 的情况下读取其 XML 格式的 PDM 文件。一次解析
得到的模型可同时用于 JSON 导出、Markdown 与离线 HTML 数据字典、物理图渲染以及
代码生成。

## 环境要求

核心类库的目标框架为 `netstandard2.0`，CLI 与测试项目为 `net8.0`。只需一个
.NET SDK 8.0 或更高版本即可还原并构建全部项目。运行时既不需要 PowerDesigner，
也不需要任何外部服务。

| 组件 | 版本 | 用途 |
| --- | --- | --- |
| .NET SDK | 8.0 或更高（`dotnet --version`） | 类库、CLI、测试 |
| Node.js | 18 或更高，含 npm | 离线 HTML 浏览器冒烟测试 |
| Playwright | Chromium，按需下载 | 离线 HTML 浏览器冒烟测试 |

其余依赖项由 NuGet 在还原阶段解析。类库使用 `Newtonsoft.Json` 13.0.4 与
`System.Text.Encoding.CodePages` 8.0.0，后者用于解码 CP936 中文编码的 RTF 注释
文本。测试项目使用 `Microsoft.NET.Test.Sdk` 17.12.0 以及 `xunit` 2.4.0 和
`xunit.runner.visualstudio` 2.4.0。`tests/browser-smoke` 仅有一个开发依赖，即
`playwright` 1.62.1。

## 安装

克隆仓库，然后还原并构建解决方案：

```powershell
git clone https://github.com/bing-framework/Bing.Pdm.git
cd Bing.Pdm
dotnet restore Bing.Pdm.slnx
dotnet build Bing.Pdm.slnx
```

用测试套件验证检出结果，它会基于 `tests/Bing.Pdm.Tests/Fixtures` 中的样例文件
运行 xUnit 测试：

```powershell
dotnet test Bing.Pdm.slnx
```

如需启用离线 HTML 浏览器冒烟测试，需一次性安装其依赖。该测试会在真实浏览器中
打开导出的 HTML，因此需要 Playwright 的 Chromium：

```powershell
cd tests/browser-smoke
npm ci
npx playwright install chromium
npm test
```

将 `PDM_BROWSER_EXECUTABLE` 设置为已安装的 Chromium 内核浏览器（如 Edge）的路径，
即可复用该浏览器而无需 Playwright 下载的 Chromium。`npm test` 会先构建一次 CLI，
再运行两组浏览器测试；首次运行前必须先还原解决方案。

仓库的 GitHub Actions 工作流会在推送和拉取请求时运行解决方案构建、测试与离线
HTML 浏览器测试。四份业务 PDM 不存放在仓库中；在持有这些文件的 Windows 机器上，
可运行以下独立验收脚本，核对模型数量、诊断、索引列、所有导出格式，并编译生成的实体：

```powershell
$pdmDir = 'path/to/private-pdm-files'
./tests/verify-real-pdm.ps1 "$pdmDir/EDI平台.pdm" "$pdmDir/物流平台.pdm" "$pdmDir/仓储管理系统.pdm" "$pdmDir/进销存平台.pdm"
```

脚本也接受只包含这四份 PDM 的目录，默认将结果保留在系统临时目录下，并在完成时打印路径。
它不会修改输入文件。

可在仓库根目录通过 `dotnet run --project` 运行 CLI，或用
`dotnet publish samples/Bing.Pdm.Tool` 发布一次后直接调用可执行文件。无需任何
配置文件；仅有的可选项是冒烟测试使用的 `PDM_BROWSER_EXECUTABLE`，以及下文所述
独立 SmartCode 适配器的 `--pdm` 与 `--output` 设置。

## 运行

核心类库的目标框架为 `netstandard2.0`，CLI 与测试项目为 `net8.0`。

```powershell
dotnet run --project samples/Bing.Pdm.Tool -- export model.pdm output json,md,html,svg,xlsx,docx zh
dotnet run --project samples/Bing.Pdm.Tool -- generate model.pdm entities Demo.Entities
dotnet run --project samples/Bing.Pdm.Tool -- generate model.pdm entities Demo.Entities --fallback-type object
dotnet test Bing.Pdm.slnx
```

导出的 JSON 可作为离线模型再次输入 CLI，无需重新读取原始 PDM：

```powershell
dotnet run --project samples/Bing.Pdm.Tool -- export model.pdm out json
dotnet run --project samples/Bing.Pdm.Tool -- export out/model.json dictionary md,html,svg zh
dotnet run --project samples/Bing.Pdm.Tool -- generate out/model.json entities Demo.Entities --fallback-type object
```

`export` 和 `generate` 根据 `.pdm` 或 `.json` 扩展名选择读取器。其他扩展名需传入
`--input-format pdm` 或 `--input-format json`；未知格式会报错。若导出文件或生成的
实体文件与输入文件路径相同，命令会在写入前拒绝覆盖。程序中可使用
`PdmJsonReader.ReadFromFile` 或 `Read(Stream)` 读取同一模型；流入口不关闭调用方的流。
修改模型对象集合后调用 `PdmInfo.RebuildLookup()` 更新按 ID 查找的索引。该操作保留
现有关系和诊断，不重新解析或校验引用。

可在 `tests/browser-smoke` 目录下用 Playwright 运行离线 HTML 浏览器冒烟测试
（`npm ci`、`npx playwright install chromium`，然后 `npm test`）。将
`PDM_BROWSER_EXECUTABLE` 设置为已安装的 Chromium 内核浏览器（如 Edge）的路径，
即可替代 Playwright 下载的 Chromium。该测试会导出仓库自带的样例文件，以本地文件
方式打开，并检查搜索、图表链接与缩放、外部请求、浏览器错误以及窄视口表现。

`export` 支持 `json`、`md`、`html`、`svg`、`xlsx` 和 `docx`；省略格式列表时默认
输出 JSON、Markdown 和 HTML。最后一个参数用于选择 `en` 或 `zh`。SVG 为每个物理图
输出一个独立文件。HTML 数据字典内嵌 CSS、JavaScript 与 SVG，提供搜索、表/视图/
物理图目录、图表缩放，以及图元与字典条目之间的跳转链接。Excel 工作簿包含表、列、
键与索引、关系、视图、视图列和诊断等工作表。Word 数据字典以横向页面承载相同的
结构信息，并跨页重复表头。两者均以 OOXML 生成，不依赖外部服务或组件。

`generate` 为每张表生成一个 C# 实体。示例类型映射可识别常见 SQL 类型，并按 PDM 的
DBMS 划分供应商特定类型的作用范围。可重复添加 `--map` 选项，例如
`--map MSSQLSRV:geography=My.App.Geography`，用于覆盖某个 DBMS 下的类型；用 `*`
代替 DBMS 即为全局覆盖。映射也可通过 CLI 程序集中的公共类 `CSharpTypeMapper`
注册。生成前会先扫描全部列再写文件。默认情况下，所有缺失与不受支持的类型会一并
报告，且不会创建输出目录。`--fallback-type object` 会显式地将每个无法解析的类型
替换为 `object`，并打印 `TYPE_FALLBACK [Table.Column]` 警告。它从不猜测拼写错误
的 SQL 类型。映射优先级依次为：DBMS 自定义精确/基础匹配、全局自定义精确/基础匹配、
厂商及通用内置映射，最后是显式兜底。已知 CLR 值类型用于可空列时会添加 `?`，
`void` 等不能用作属性类型的名称会被拒绝。CLI 程序集还公开了 `CSharpTypeMapper.RegisterFallback`、
`Resolve` 以及 `EntityGenerator.GenerateWithDiagnostics`。以编程方式调用时，可向
`EntityGenerator` 传入 `ICSharpEntityTemplate` 以控制源代码布局；其上下文包含无
冲突的名称、已映射的 CLR 类型以及原始的表/列对象。

`samples/Bing.PdmGenerateDemo` 中的独立 SmartCode 2.2 适配器面向 .NET 8，接受
`--pdm` 与 `--output`（或 `appsettings.json` 中的对应配置）。它将同一份 `PdmInfo`
的表和列映射为 SmartCode 的表模型。其包依赖图仍包含旧版依赖，其中有被 NuGet 报告
存在漏洞的包，因此请以上述 .NET 8 CLI 作为受支持的生成途径。该适配器不加入解决
方案，仅作为兼容性示例保留。

## 使用示例

以下示例使用仓库自带的样例文件 `tests/Bing.Pdm.Tests/Fixtures/complete.pdm`，
这是一个 SQL Server 模型，包含两张表、一个视图、一个关系，以及一处故意构造的失效
父表引用。CLI 将生成的文件路径写入标准输出，将诊断信息写入标准错误。

### 以全部格式导出数据字典

```powershell
dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/complete.pdm out json,md,html,xlsx,docx zh
```

```
out/complete.json
out/complete.md
out/complete.html
out/complete.xlsx
out/complete.docx
```

Markdown 数据字典逐表列出其列、键与索引，随后是关系、视图和诊断。`Name` 保存显示
名称，`Column` 保存代码，因此双语模型在任一语言下都可读。以下是 `out/complete.md`
的节选，已移除注释列与行版本列：

```markdown
## Order Order

| Column | Name | Type | Length | Precision | Required | PK | Default | Comment |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OrderId | Order ID | int |  |  | Yes | Yes |  |  |
| CustomerId | Customer ID | int |  |  | Yes | No |  |  |
| Total | Total | decimal | 12 | 2 | Yes | No |  |  |
```

HTML 文件内嵌 CSS、JavaScript 与 SVG，因此无需服务器即可从磁盘直接打开。Excel
工作簿包含表、列、键与索引、关系、视图、视图列和诊断等工作表；Word 文档采用横向
版式，并在跨页时重复表头。

### 将物理图导出为 SVG

```powershell
dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/complete.pdm out svg
```

```
out/complete-Overview.svg
```

每个物理图输出一个独立的 SVG 文件，文件名由输入文件名加上物理图的代码或名称构成。
模型中若没有物理图，则会失败并提示
`The model has no physical diagrams to export.`

### 生成 C# 实体

```powershell
dotnet run --project samples/Bing.Pdm.Tool -- generate tests/Bing.Pdm.Tests/Fixtures/complete.pdm gen Demo.Entities
```

每张表在 `gen` 目录下生成一个文件。`gen/Customer.cs` 的内容如下：

```csharp
// Generated from PDM. Review database-specific types before use.
using System;

namespace Demo.Entities;

public class Customer
{
    public int TenantId { get; set; }
    public int CustomerId { get; set; }
    public string Name { get; set; }
}
```

名称会转换为无冲突的 PascalCase 标识符，C# 关键字会被转义，与类名冲突的属性会
追加后缀。默认模板会限定 `DateTime`、`Guid` 等系统类型，避免同名实体遮蔽它们。
省略命名空间时默认使用 `Generated.Entities`。

### 覆盖供应商特定类型

假设 `model.pdm` 含有 `geography` 列。内置映射未涵盖的供应商类型，可通过可重复的
`--map <dbms|*>:<database-type>=<C#-type>` 按 DBMS 进行映射：

```powershell
dotnet run --project samples/Bing.Pdm.Tool -- generate model.pdm gen Demo.Entities --map MSSQLSRV:geography=My.App.Geography
```

该列将变为 `public My.App.Geography Name { get; set; }`。用 `*` 代替 DBMS 代码
即为全局覆盖。优先级依次为：DBMS 自定义精确/基础匹配、全局自定义精确/基础匹配、
厂商及通用内置映射，最后是显式兜底。

### 使用前先查看诊断信息

在写入任何文件之前，会先解析每一列的类型。未配置兜底时，运行会报告每个无法解析的
类型，以退出码 1 结束，且不创建输出目录：

```
TYPE_ERROR [Customer.Name]: Unsupported database type 'geography' for DBMS 'Microsoft SQL Server' at column 'Name'.
```

添加 `--fallback-type object` 后，将替换为兜底类型，每列打印一条警告，并继续写入
实体，该列因此变为 `public object Name { get; set; }`：

```
TYPE_FALLBACK [Customer.Name]: Unsupported database type 'geography' for DBMS 'Microsoft SQL Server' at column 'Name'.
```

导出时也会以同样方式报告模型问题，例如
`UNRESOLVED_REF [o62]: parent table reference 'o999' cannot be resolved.` 以及
`UNSUPPORTED_DIAGRAM_SYMBOL [unknown]: Visible diagram symbol type
'UnfamiliarSymbol' is not supported.`。这些条目同样会写入每种导出结果的诊断部分，
因此在依赖生成的结构之前应先检查它们。

## 类库 API

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

自定义导出标签会覆盖内置的英文或中文标签。缺失或为空的自定义值会回退到所选语言。
若要替换 Markdown 或 HTML 的版式，请实现 `IPdmExportTemplate`；导出器会将解析后的
`PdmInfo`、同一份已解析的标签提供器以及输出写入器传入。JSON 与 SVG 沿用其稳定的
内置序列化器。

`IPdmReader.Read(Stream)` 不会关闭传入的流。`PdmInfo` 将根级对象与递归包分开保存。
`AllTables`、`AllViews`、`AllReferences`、`AllShortcuts` 和 `AllPhysicalDiagrams`
提供扁平化视图。原始 PDM ID 予以保留，关联关系使用 ID 而非对象指针，因此 JSON 中
不存在引用环。`Lookup` 在解析完成后提供基于 ID 的查找，涵盖表、包、视图、关系、
列、键、所有者、物理图以及图元和快捷方式。快捷方式保留其原始 PDM ID、ObjectID、
TargetID 与解析后的目标 ID。关系与表图元将原始 Shortcut 引用与归一化后的真实表
ID 分开保存。

解析器处理模型元数据、包、表、列、键、索引、视图、关系与连接、表快捷方式、
`IndexColumns/IndexColumn` 以及物理图图元。它接受 PDM 各变体中出现的集合名称
`Columns`/`ColumnInfos`、`Keys`/`KeyInfos` 和 `Indexes`/`IndexeInfos`。它拒绝
XML DTD 与外部实体。缺少模型节点或 XML 格式错误会导致失败；缺失的对象引用会在
`PdmInfo.Diagnostics` 中产生 `UNRESOLVED_REF` 条目，缺失必需引用则产生
`MISSING_REF`。重复 ID、格式错误的物理图坐标以及不完整的复合外键连接，分别报告为
`DUPLICATE_ID`、`MALFORMED_GEOMETRY` 和 `FOREIGN_KEY_KEY_MISMATCH`。诊断信息包含
在 JSON、Markdown、HTML、Excel 和 Word 的导出结果中。使用者在依赖生成的结构之前
应检查这些信息。处于激活状态但目标无法解析的快捷方式会产生
`UNRESOLVED_SHORTCUT_TARGET`；目标 ObjectID 存在歧义则产生
`AMBIGUOUS_SHORTCUT_TARGET`。未知的可见图元类型会产生 `UNSUPPORTED_DIAGRAM_SYMBOL`。

物理图渲染表、包、注释、文本、椭圆、折线、预定义形状、架构区域、注释链接与扩展
依赖关系。嵌套的 `SubSymbols` 会递归遍历。矩形、连接路径以及基本的 PowerDesigner
BGR `COLORREF` 颜色值予以保留。RTF 注释内容会转为转义后的纯文本，包括 CP936 中文。
`TargetModel`、`Replication`、`SubReplication` 及嵌入的 `FullShortcutModel` 对象保存
在独立的元数据集合与 `MetadataLookup` 中，不会增加主模型的业务表数量。缺失的复制来源、
副本和会话引用分别产生诊断；读取不执行复制同步或属性继承。

`IPdmExporter.Write` 支持 JSON、Markdown、HTML 或单个 SVG 物理图。`WriteDiagram`
用于写出指定的独立 SVG。`PdmOfficeExporter.WriteExcel` 与 `WriteWord` 写入由调用
方持有的流，并且不会关闭这些流。

物理图保留图元矩形与连接线折点。HTML 中的 SVG 会将 PDM 的纵轴翻转为浏览器坐标系，
并采用一致的显示样式。可选 `PowerDesigner` 模式会读取 `FontList`、渐变、阴影、线型和
表内字段显示信息；当前为基于 16.7.4 的基础样式还原，尚未通过人工像素级验收。
原生模式已还原主键、替代键和多索引清单；RTF 分段保留字体、前景色和下划线，
阴影按图元读取颜色。触发器清单读取 `Time`、`Event`、`Text` 并保留原始属性。
RTF 段落支持对齐、按文字单元换行和图元内裁剪；表字段沿模型列顺序显示。
Windows CLI 使用本机 GDI 字体宽度度量；程序调用方可通过 `MeasureText` 提供同单位的测量器。
未提供有效测量器时记录 `APPROXIMATE_FONT_METRICS`，继续估算宽度。8pt 小字号已按原生 SVG 校准。
支持全部列、主键列和键列过滤；自定义表达式可通过 `ColumnFilter` 显式解释。
无法解释的表达式保留原文、显示全部列并报告 `UNSUPPORTED_DIAGRAM_STYLE`，不执行任意过滤脚本。
渲染选项的 `Diagnostics` 会指出缺失字体和未解释的样式；出现这些诊断时
`CanCompareToNative` 为 `false`。CLI 的原生模式会将这些诊断写入标准错误。
关系图元可显示 PDM 中显式保存的 `ForeignKeyConstraintName`。若显示选项要求该名称，
但 PDM 未保存 PowerDesigner 计算出的名称，则使用引用 Code 并报告
`UNRESOLVED_REFERENCE_LABEL`；此时不具备原生视觉对照资格。
受支持的格式为以 `templates/pdm_template.xml` 和测试样例文件为代表的 XML PDM 家族。

## 版本化 JSON 与跨模型工作区

新 JSON 根对象有 `SchemaVersion: 1`；旧导出缺少版本号时视为版本 0。
`PdmJsonReader` 在内存迁移后读取，`PdmJsonMigrator.Migrate(Stream, TextWriter)` 可保留
未知 JSON 字段生成新文件。版本与 PDM 模型的 `Version` 无关。格式说明见
`docs/schemas/pdm-v1.schema.json`，目前只支持本项目导出的 JSON。
迁移与读取共用固定模型字段类型校验：损坏的已知字段、空集合元素和重复 JSON 属性会被拒绝，
可选地址与矩形可为 `null`；缺失可选集合仍为空集合。校验不负责对象 ID 唯一性和完整关系校验。
未知字段仅承诺在节点级 `Migrate` 中保留；读取为 `PdmInfo` 再导出会忽略未建模字段。

```powershell
dotnet run --project samples/Bing.Pdm.Tool -- migrate old.json current.json
dotnet run --project samples/Bing.Pdm.Tool -- export current.json output json,md,html
```

工作区清单必须显式列出本地 `.pdm` 或 `.json`；路径相对清单文件。仓库内的
`tests/Bing.Pdm.Tests/Fixtures/workspace.json` 是可运行示例。不会根据
`TargetModelURL` 下载或自动打开其他文件。工作区中重复键、重复模型 GUID 或不可读取的
模型会使加载失败；相同的 PDM 对象 ID 可在不同模型中重复。

```powershell
dotnet run --project samples/Bing.Pdm.Tool -- export tests/Bing.Pdm.Tests/Fixtures/workspace-sales.pdm output json,md,html en --workspace tests/Bing.Pdm.Tests/Fixtures/workspace.json
dotnet run --project samples/Bing.Pdm.Tool -- generate tests/Bing.Pdm.Tests/Fixtures/workspace-sales.pdm entities Sample.Entities --workspace tests/Bing.Pdm.Tests/Fixtures/workspace.json
```

`PdmWorkspaceResolver` 在清单内完成 Shortcut、外键端点、列及图元的限定模型解析。
`PdmObjectAddress` 的 `ModelKey` 与 `PdmId` 一起标识对象；未提供工作区时，JSON
读取不会自行加载外部模型。业务对象集合修改后调用 `RebuildLookup()` 重建本模型索引，
此操作不会重新运行跨模型解析或完整校验。
`new PdmModelValidator().Validate(model, workspace, modelKey)` 独立检查当前集合的身份、
引用类型和归属，不依赖可能已过期的 Lookup，也不修改模型诊断。
结果区分 `Structure`、`Diagram`、`Metadata`；缺少显式依赖时外部地址标记为未验证。
GUID 的大小写、花括号和 D 格式归一化用于匹配，原始字段仍保留。

## 结构差异与图形对照

`PdmModelComparer.Compare(before, after, options)` 只比较数据库结构，不生成迁移 SQL。
默认按唯一 `ObjectID`、再按模式/包/代码路径配对；名称区分大小写，可通过
`PdmCompareOptions.IgnoreCase` 调整。报告忽略图形布局和复制元数据，歧义配对标记
`Incomplete`。列顺序、主键、索引和本模型外键复用对象配对结果，因此仅重建 GUID 不会造成
关系变化误报；不同包的同名引用按包路径区分，保持 GUID 的引用跨包移动会列出两侧路径。
通过 `BeforeWorkspace` / `AfterWorkspace` 和对应模型键提供两侧依赖时，外部地址按
依赖模型 GUID 与对象 GUID 比较，清单别名变化不会误报。没有依赖上下文时保留地址比较，
并通过 `ValidationIssues` 与 `Incomplete` 明确标记未验证的端点；图形与元数据问题不影响结构比较完整性。
CLI 可混用 PDM 和 JSON：

```powershell
dotnet run --project samples/Bing.Pdm.Tool -- diff before.pdm after.json diff-output json,md,html --fail-on-change --fail-on-incomplete

# 两侧使用独立的显式工作区，模型键可以不同
dotnet run --project samples/Bing.Pdm.Tool -- diff before.pdm after.json diff-output json --before-workspace before-workspace.json --after-workspace after-workspace.json
dotnet run --project samples/Bing.Pdm.Tool -- export model.pdm output html,svg en --diagram-style powerdesigner
```

`diff` 默认返回 0；使用 `--fail-on-change` 时有变化返回 3，参数错误返回 2，读取或
比较失败返回 1。不完整报告默认仍写出并返回 0，同时输出 `INCOMPLETE_COMPARISON`；
`--fail-on-incomplete` 使不完整比较返回 1，优先于有变化的返回码 3。
JSON、Markdown 和 HTML 都列出不完整原因。报告列出两侧 DBMS，不判断数据库迁移是否安全。原生脱敏图和当前
渲染图及差异统计位于 `docs/visual-baseline`，人工视觉确认仍待完成。

## 输出覆盖与失败恢复

CLI 的 export、generate、diff 和 migrate 先写独立暂存目录，再发布全部计划文件。
成功时覆盖同名计划文件，保留目录内其他文件；中途异常回滚已发布文件并恢复原文件。
回滚失败会保留恢复目录并打印路径。输入与输出路径冲突仍会拒绝。
该策略处理进程内失败，不承诺断电、强制终止或多个进程同时写同一目录的跨文件原子性。
实体类型预检失败仍不创建输出目录或部分实体。验收脚本每次创建独立输出子目录。

## 贡献指南

欢迎提交 Issue 与拉取请求。默认分支为 `master`。

### 代码规范

仓库中没有 `.editorconfig`，因此请遵循现有代码中的约定：

- 目标框架固定：`src/Bing.Pdm` 保持 `netstandard2.0` 且 `LangVersion` 为 7.1，
  CLI 与测试保持 `net8.0`。不要提升类库的目标框架，也不要在其中使用高于
  `netstandard2.0` 的 API。
- 使用四个空格缩进，并以 UTF-8 保存。换行符由 `.gitattributes` 统一规范化
  （`* text=auto`），生成的文本使用不带 BOM 的 `UTF8Encoding` 写出。
- 为每一个公开类型和成员编写 XML 文档注释，如同 `PdmExporter`、
  `PdmOfficeExporter` 和 `EntityGenerator` 那样。仓库内的摘要使用中文；修改文件
  时请与其语言保持一致。
- 保持构建无警告。当前 `dotnet build Bing.Pdm.slnx` 输出 0 个警告、0 个错误，
  一项修改只有在维持这两个数字为零时才算完成。
- 公开 API 的改动应保持增量式。超出支持范围的行为应在文档中明确说明，而不是被
  静默丢弃。
- 新增解析行为需要配套的模型类、解析器改动、在输入可能不完整时使用的诊断代码，
  以及测试。可恢复的问题应通过 `PdmInfo.Diagnostics` 报告，而不是抛出异常。

### 测试

- 在 `tests/Bing.Pdm.Tests` 下添加 xUnit 测试。小型的合成 PDM 文件放在
  `tests/Bing.Pdm.Tests/Fixtures` 中，并通过已有的 `PreserveNewest` 项复制到
  输出目录，因此无需修改项目文件。
- 提交拉取请求前运行 `dotnet test Bing.Pdm.slnx`。
- 修改离线 HTML 数据字典时，还需运行「安装」一节所述的浏览器冒烟测试。该测试会
  导出仓库自带的样例文件，以本地文件方式打开，并检查搜索、图表链接与缩放、外部
  请求、浏览器错误以及窄视口表现。
- 持有四份业务 PDM 时，使用 `tests/verify-real-pdm.ps1` 运行本地兼容性验收；CI
  只使用仓库内可公开的样例文件。

### 提交与拉取请求

提交信息遵循 Conventional Commits，与现有提交历史一致
（`feat: 新增 PDM 模型`、`✨ feat(pdm): 重构PDM解析并新增多格式导出`）。使用
`feat`、`fix`、`docs`、`refactor`、`test` 或 `chore`，当改动集中于某一领域时可
附加作用域，例如 `pdm`。

1. 复刻（fork）仓库，并从 `master` 创建分支。
2. 完成改动并添加或更新测试，同时保持构建无警告。
3. 使用符合 Conventional Commits 的提交信息提交。
4. 推送分支并创建拉取请求，说明改动内容、受影响的格式或诊断，以及验证方式：
   解决方案测试、浏览器冒烟测试，或真实的 PDM 文件。
