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
dotnet restore Bing.Pdm.sln
dotnet build Bing.Pdm.sln
```

用测试套件验证检出结果，它会基于 `tests/Bing.Pdm.Tests/Fixtures` 中的样例文件
运行 xUnit 测试：

```powershell
dotnet test Bing.Pdm.sln
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
即可复用该浏览器而无需 Playwright 下载的 Chromium。冒烟测试通过
`dotnet run --no-restore` 调用 CLI，因此首次运行 `npm test` 之前必须先还原解决方案。

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
dotnet test Bing.Pdm.sln
```

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
的 SQL 类型。映射优先级依次为：DBMS 精确/基础匹配、DBMS 内置映射、全局精确/基础
匹配，最后是显式兜底。CLI 程序集还公开了 `CSharpTypeMapper.RegisterFallback`、
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
dotnet run --project samples/Bing.Pdm.Tool -- export model.pdm out json,md,html,xlsx,docx zh
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
dotnet run --project samples/Bing.Pdm.Tool -- export model.pdm out svg
```

```
out/complete-Overview.svg
```

每个物理图输出一个独立的 SVG 文件，文件名由输入文件名加上物理图的代码或名称构成。
模型中若没有物理图，则会失败并提示
`The model has no physical diagrams to export.`

### 生成 C# 实体

```powershell
dotnet run --project samples/Bing.Pdm.Tool -- generate model.pdm gen Demo.Entities
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
追加后缀。省略命名空间时默认使用 `Generated.Entities`。

### 覆盖供应商特定类型

内置映射未涵盖的供应商类型，可通过可重复的
`--map <dbms|*>:<database-type>=<C#-type>` 按 DBMS 进行映射：

```powershell
dotnet run --project samples/Bing.Pdm.Tool -- generate model.pdm gen Demo.Entities --map MSSQLSRV:geography=My.App.Geography
```

该列将变为 `public My.App.Geography Name { get; set; }`。用 `*` 代替 DBMS 代码
即为全局覆盖。优先级依次为：DBMS 精确/基础匹配、DBMS 内置映射、全局精确/基础
匹配，最后是显式兜底。

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
BGR `COLORREF` 颜色值予以保留；视觉效果与字体保真度不在支持范围内。RTF 注释内容
会转为转义后的纯文本，包括 CP936 中文。`TargetModel`、`Replication` 与
`SubReplication` 不属于统一模型。快捷方式只能解析到当前根模型中的对象。

`IPdmExporter.Write` 支持 JSON、Markdown、HTML 或单个 SVG 物理图。`WriteDiagram`
用于写出指定的独立 SVG。`PdmOfficeExporter.WriteExcel` 与 `WriteWord` 写入由调用
方持有的流，并且不会关闭这些流。

物理图保留图元矩形与连接线折点。HTML 中的 SVG 会将 PDM 的纵轴翻转为浏览器坐标系，
并采用一致的显示样式；它不还原 PowerDesigner 的字体、颜色、阴影或全部图元类型。
受支持的格式为以 `templates/pdm_template.xml` 和测试样例文件为代表的 XML PDM 家族。

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
- 保持构建无警告。当前 `dotnet build Bing.Pdm.sln` 输出 0 个警告、0 个错误，
  一项修改只有在维持这两个数字为零时才算完成。
- 公开 API 的改动应保持增量式。超出支持范围的行为应在文档中明确说明，而不是被
  静默丢弃。
- 新增解析行为需要配套的模型类、解析器改动、在输入可能不完整时使用的诊断代码，
  以及测试。可恢复的问题应通过 `PdmInfo.Diagnostics` 报告，而不是抛出异常。

### 测试

- 在 `tests/Bing.Pdm.Tests` 下添加 xUnit 测试。小型的合成 PDM 文件放在
  `tests/Bing.Pdm.Tests/Fixtures` 中，并通过已有的 `PreserveNewest` 项复制到
  输出目录，因此无需修改项目文件。
- 提交拉取请求前运行 `dotnet test Bing.Pdm.sln`。
- 修改离线 HTML 数据字典时，还需运行「安装」一节所述的浏览器冒烟测试。该测试会
  导出仓库自带的样例文件，以本地文件方式打开，并检查搜索、图表链接与缩放、外部
  请求、浏览器错误以及窄视口表现。

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
