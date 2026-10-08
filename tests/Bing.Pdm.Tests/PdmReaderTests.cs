using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Bing.Pdm;
using Bing.Pdm.Models;
using Bing.Pdm.Models.PhysicalDiagrams;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Reader;
using Bing.Pdm.Tool;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证 PDM 读取、导出和代码生成行为。
    /// </summary>
    public class PdmReaderTests
    {
        /// <summary>
        /// 获取完整 PDM fixture 的路径。
        /// </summary>
        private static string Fixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "complete.pdm");

        /// <summary>
        /// 验证层级对象、键、索引和引用读取。
        /// </summary>
        [Fact]
        public void ReadsHierarchyKeysIndexesAndReferences()
        {
            var model = new PdmReader().ReadFromFile(Fixture);

            Assert.Equal("FixtureModel", model.Code);
            Assert.Single(model.Tables);
            Assert.Equal("o40", model.Packages.Single().Id);
            Assert.Equal("o41", model.Packages.Single().Packages.Single().Id);
            Assert.Equal(2, model.AllTables.Count());
            Assert.Single(model.AllViews);
            Assert.Equal(2, model.AllReferences.Count());
            Assert.Equal("MSSQLSRV", model.DbmsCode);
            Assert.Single(model.Schemas);
            Assert.Equal("o80", model.Schemas.Single().SchemaId);

            var customer = model.AllTables.Single(x => x.Id == "o50");
            Assert.Equal("o41", customer.PackageId);
            Assert.Equal("o80", customer.OwnerId);
            Assert.Equal("o80", customer.SchemaId);
            Assert.Equal("o54", customer.PrimaryKeyId);
            Assert.Equal(new[] { "o51", "o52" }, customer.Keys.Single().ColumnIds);
            Assert.True(customer.Columns[0].PrimaryKey);
            Assert.True(customer.Columns[1].PrimaryKey);
            Assert.False(customer.Columns[2].PrimaryKey);
            Assert.Equal("o53", customer.Indexes.Single().ColumnIds.Single());
            Assert.True(customer.Indexes.Single().Unique);
            Assert.True(model.Lookup.TryGetTable(customer.Id, out var indexedCustomer));
            Assert.Same(customer, indexedCustomer);

            var reference = model.AllReferences.Single(x => x.Id == "o60");
            Assert.Equal("o50", reference.ParentTableId);
            Assert.Equal("o30", reference.ChildTableId);
            Assert.Equal(2, reference.Joins.Count);
            Assert.Contains(reference.Joins, x => x.ParentColumnId == "o52" && x.ChildColumnId == "o32");
            Assert.Contains(reference.Joins, x => x.ParentColumnId == "o51" && x.ChildColumnId == "o37");
            var displayNote = model.Tables[0].Columns.Single(x => x.Id == "o33");
            Assert.Equal("R&D <priority> \"high\"", displayNote.Comment);
            Assert.Equal("N'pending'", displayNote.DefaultValue);
            Assert.Equal("2", model.Tables[0].Columns.Single(x => x.Id == "o36").Precision);
            Assert.Contains(model.Diagnostics, x => x.Code == "UNRESOLVED_REF" && x.Message.Contains("o999"));
            Assert.DoesNotContain(model.Diagnostics, x => x.Code == "FOREIGN_KEY_KEY_MISMATCH");
            var view = model.AllViews.Single();
            Assert.Equal("o70", view.Id);
            Assert.Equal("o71", view.Columns[0].Id);
            Assert.Equal("o70", view.Columns[0].ViewId);
            Assert.Equal("o72", view.Columns[1].Id);
        }

        /// <summary>
        /// 验证物理图几何数据并保持调用方流可用。
        /// </summary>
        [Fact]
        public void ReadsGeometryAndKeepsCallerStreamOpen()
        {
            using (var stream = File.OpenRead(Fixture))
            {
                var model = new PdmReader().Read(stream);
                Assert.True(stream.CanRead);
                var diagram = model.PhysicalDiagrams.Single();
                var line = diagram.Symbols.Single(x => x.Id == "o24");
                Assert.Equal("o60", line.ObjectId);
                Assert.Equal("o21", line.SourceSymbolId);
                Assert.Equal("o22", line.DestinationSymbolId);
                Assert.Equal(6, line.Points.Count);
                Assert.Equal(1650, line.Points[2].X);
                Assert.Equal(300, line.Points[2].Y);
                Assert.Equal(100, diagram.Symbols.Single(x => x.Id == "o21").Rect.X1);
                Assert.Equal(2200, diagram.Symbols.Single(x => x.Id == "o22").Rect.X1);
                Assert.Equal(300, line.Rect.Y1);
                Assert.Equal("o41", diagram.Symbols.Single(x => x.Id == "o23").ObjectId);
                Assert.True(model.Lookup.TryGetSymbol("o24", out var indexedSymbol));
                Assert.Same(line, indexedSymbol);
            }
        }

        /// <summary>
        /// 验证缺少模型和外部实体的输入会被拒绝。
        /// </summary>
        [Fact]
        public void RejectsMissingModelAndExternalEntities()
        {
            using (var empty = new MemoryStream(Encoding.UTF8.GetBytes("<root/>")))
                Assert.Throws<InvalidDataException>(() => new PdmReader().Read(empty));
            using (var dtd = new MemoryStream(Encoding.UTF8.GetBytes("<!DOCTYPE Model [<!ENTITY x SYSTEM 'file:///secret'>]><Model/>")))
                Assert.Throws<System.Xml.XmlException>(() => new PdmReader().Read(dtd));
        }

        /// <summary>
        /// 验证非法 XML 返回明确解析错误。
        /// </summary>
        [Fact]
        public void RejectsMalformedXmlWithExplicitParseError()
        {
            using (var malformed = new MemoryStream(Encoding.UTF8.GetBytes("<Model xmlns:o=\"object\"><o:RootObject>")))
            {
                var exception = Assert.Throws<XmlException>(() => new PdmReader().Read(malformed));
                Assert.Contains("unexpected end", exception.Message.ToLowerInvariant());
                Assert.True(exception.LineNumber > 0);
            }
        }

        /// <summary>
        /// 验证 JSON、Markdown 和 HTML 导出内容一致且离线可用。
        /// </summary>
        [Fact]
        public void ExportsConsistentOfflineDictionary()
        {
            var model = new PdmReader().ReadFromFile(Fixture);
            var diagram = model.PhysicalDiagrams.Single();
            var exporter = new PdmExporter();
            using (var json = new StringWriter())
            using (var markdown = new StringWriter())
            using (var html = new StringWriter())
            {
                exporter.Write(model, PdmExportFormat.Json, json);
                exporter.Write(model, PdmExportFormat.Markdown, markdown);
                exporter.Write(model, PdmExportFormat.Html, html);

                var document = JObject.Parse(json.ToString());
                Assert.Equal("FixtureModel", (string)document["Code"]);
                Assert.Single(document["Tables"]);
                Assert.Single(document["Packages"]);
                Assert.Equal(2, document["References"].Count());

                var expectedTables = model.AllTables.ToArray();
                var expectedTableCount = expectedTables.Length;
                var expectedColumnCount = expectedTables.Sum(table => table.Columns.Count);
                var jsonTables = JsonTables(document).ToArray();
                Assert.Equal(expectedTableCount, jsonTables.Length);
                Assert.Equal(expectedColumnCount, jsonTables.Sum(table => ((JArray)table["Columns"]).Count));

                var markdownText = markdown.ToString();
                var markdownSections = expectedTables
                    .Select(table => ExtractMarkdownTableSection(model, markdownText, table))
                    .ToArray();
                Assert.Equal(expectedTableCount, markdownSections.Length);
                Assert.Equal(expectedColumnCount, markdownSections.Sum(CountMarkdownColumnRows));

                var htmlText = html.ToString();
                var htmlArticles = expectedTables
                    .Select(table => ExtractHtmlTableArticle(htmlText, table.Id))
                    .ToArray();
                Assert.Equal(expectedTableCount, htmlArticles.Length);
                Assert.Equal(expectedColumnCount, htmlArticles.Sum(CountHtmlColumnRows));

                Assert.Contains("Sales / CRM / Customer", markdown.ToString());
                Assert.Contains("Table comment &amp; notes\\.", markdown.ToString());
                Assert.Contains("Primary key PK\\_Customer: TenantId, CustomerId", markdown.ToString());
                Assert.Contains("unique", markdown.ToString());
                Assert.Contains("Identity", markdown.ToString());
                Assert.Contains("DisplayNote", markdown.ToString());
                Assert.Contains("R&amp;D &lt;priority&gt;", markdown.ToString());
                Assert.Contains("SELECT OrderId, CustomerId FROM Order", markdown.ToString());
                Assert.Contains("data-searchable", html.ToString());
                Assert.Contains("dictionary-search", html.ToString());
                Assert.Contains("data-zoom=\"in\"", html.ToString());
                Assert.Contains("class=\"toc\"", html.ToString());
                Assert.Contains("Sales Schema", html.ToString());
                Assert.Contains("R&amp;D &lt;priority&gt;", html.ToString());
                Assert.Contains("<pre><code>SELECT OrderId, CustomerId FROM Order</code></pre>", html.ToString());
                Assert.Contains("data-symbol-id=\"o24\"", html.ToString());
                Assert.Contains("points=\"1540,890", html.ToString());
                Assert.Contains("href=\"#table-", html.ToString());
                Assert.Contains("href=\"#reference-", html.ToString());
                Assert.Contains("<script>", html.ToString());
                Assert.DoesNotContain("src=\"http", html.ToString());
                Assert.DoesNotContain("href=\"https://", html.ToString());
                var markup = html.ToString();
                var svg = markup.Substring(markup.IndexOf("<svg ", StringComparison.Ordinal),
                    markup.IndexOf("</svg>", StringComparison.Ordinal) - markup.IndexOf("<svg ", StringComparison.Ordinal) + 6);
                var svgDocument = new XmlDocument { XmlResolver = null };
                svgDocument.LoadXml(svg);
                Assert.Equal("svg", svgDocument.DocumentElement.LocalName);

                var chineseHtml = new StringWriter();
                new PdmExporter(PdmExportLanguage.Chinese).Write(model, PdmExportFormat.Html, chineseHtml);
                Assert.Contains("数据表", chineseHtml.ToString());
                Assert.Contains("搜索", chineseHtml.ToString());

                var standaloneSvg = new StringWriter();
                exporter.WriteDiagram(model, diagram, standaloneSvg);
                var standaloneDocument = new XmlDocument { XmlResolver = null };
                standaloneDocument.LoadXml(standaloneSvg.ToString());
                Assert.Equal("svg", standaloneDocument.DocumentElement.LocalName);
                Assert.DoesNotContain("<a href=", standaloneSvg.ToString());
            }
        }

        /// <summary>
        /// 验证仓库模板在引用不完整时仍可读取。
        /// </summary>
        [Fact]
        public void ReadsRepositoryTemplateDespiteIncompleteReferences()
        {
            var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "templates", "pdm_template.xml"));
            var model = new PdmReader().ReadFromFile(path);
            Assert.NotEmpty(model.AllTables);
            Assert.NotEmpty(model.AllTables.First().Columns);
            Assert.NotEmpty(model.AllTables.First().Keys);
            Assert.NotEmpty(model.AllPhysicalDiagrams);
            Assert.NotEmpty(model.Diagnostics);
        }

        /// <summary>
        /// 验证重复标识、缺失引用、非法几何和复合键异常诊断。
        /// </summary>
        [Fact]
        public void DiagnosesDuplicateIdsMissingReferencesMalformedGeometryAndBrokenCompositeKeys()
        {
            var xml = File.ReadAllText(Fixture);
            var duplicate = xml.Replace("<o:ReferenceJoin Id=\"o63\"", "<o:ReferenceJoin Id=\"o61\"");
            Assert.Contains(new PdmReader().Read(new MemoryStream(Encoding.UTF8.GetBytes(duplicate))).Diagnostics,
                x => x.Code == "DUPLICATE_ID" && x.SourceId == "o61");

            var missing = xml.Replace("<c:ParentTable><o:Table Ref=\"o999\"/></c:ParentTable>", "<c:ParentTable/>");
            Assert.Contains(new PdmReader().Read(new MemoryStream(Encoding.UTF8.GetBytes(missing))).Diagnostics,
                x => x.Code == "MISSING_REF" && x.SourceId == "o62");

            var malformedGeometry = xml.Replace("((1400,300),(2200,650))", "((bad,300),(2200,650))");
            Assert.Contains(new PdmReader().Read(new MemoryStream(Encoding.UTF8.GetBytes(malformedGeometry))).Diagnostics,
                x => x.Code == "MALFORMED_GEOMETRY" && x.SourceId == "o24");

            var incompleteJoin = xml.Replace("<o:ReferenceJoin Id=\"o63\"><a:ObjectID>00000000-0000-0000-0000-000000000063</a:ObjectID><c:Object1><o:Column Ref=\"o51\"/></c:Object1><c:Object2><o:Column Ref=\"o37\"/></c:Object2></o:ReferenceJoin>", string.Empty);
            Assert.Contains(new PdmReader().Read(new MemoryStream(Encoding.UTF8.GetBytes(incompleteJoin))).Diagnostics,
                x => x.Code == "FOREIGN_KEY_KEY_MISMATCH" && x.SourceId == "o60");
        }

        /// <summary>
        /// 验证 Excel 和 Word 文档包结构有效。
        /// </summary>
        [Fact]
        public void WritesValidExcelAndWordPackages()
        {
            var model = new PdmReader().ReadFromFile(Fixture);
            var exporter = new PdmOfficeExporter(PdmExportLanguage.Chinese);
            using (var excel = new MemoryStream())
            {
                exporter.WriteExcel(model, excel);
                excel.Position = 0;
                using (var archive = new ZipArchive(excel, ZipArchiveMode.Read, true))
                {
                    Assert.Contains(archive.Entries, x => x.FullName == "xl/worksheets/sheet7.xml");
                    var workbook = ReadXml(archive, "xl/workbook.xml");
                    XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    var sheets = workbook.Root.Element(spreadsheet + "sheets").Elements(spreadsheet + "sheet").ToArray();
                    Assert.Equal(7, sheets.Length);
                    Assert.Contains(sheets, sheet => (string)sheet.Attribute("name") == "字段");
                    var firstWorksheet = ReadXml(archive, "xl/worksheets/sheet1.xml");
                    var pageSetup = firstWorksheet.Root.Element(spreadsheet + "pageSetup");
                    Assert.Equal("landscape", (string)pageSetup.Attribute("orientation"));
                    Assert.Equal("1", (string)pageSetup.Attribute("fitToWidth"));
                    Assert.Equal("0", (string)pageSetup.Attribute("fitToHeight"));
                    Assert.Equal("1", (string)firstWorksheet.Root.Element(spreadsheet + "sheetPr")
                        .Element(spreadsheet + "pageSetUpPr").Attribute("fitToPage"));
                    var columns = ReadXml(archive, "xl/worksheets/sheet2.xml");
                    Assert.Contains("数据表 ID", columns.ToString());
                    Assert.Contains("DisplayNote", columns.ToString());
                    Assert.Contains("R&amp;D &lt;priority&gt;", columns.ToString());
                    var columnWidths = columns.Root.Element(spreadsheet + "cols").Elements(spreadsheet + "col")
                        .Select(column => (double)column.Attribute("width")).ToArray();
                    Assert.Equal(13, columnWidths.Length);
                    Assert.InRange(columnWidths.Sum(), 117, 150.1);
                    Assert.Equal("2", (string)columns.Root.Element(spreadsheet + "sheetData")
                        .Elements(spreadsheet + "row").Skip(1).First()
                        .Elements(spreadsheet + "c").First().Attribute("s"));
                    var styles = ReadXml(archive, "xl/styles.xml");
                    var wrappedStyle = styles.Root.Element(spreadsheet + "cellXfs").Elements(spreadsheet + "xf").Skip(2).Single();
                    Assert.Equal("1", (string)wrappedStyle.Element(spreadsheet + "alignment").Attribute("wrapText"));
                    Assert.Equal("top", (string)wrappedStyle.Element(spreadsheet + "alignment").Attribute("vertical"));
                    var references = ReadXml(archive, "xl/worksheets/sheet4.xml");
                    Assert.Contains("FK_Missing_Parent_Diagnostic", references.ToString());
                    var views = ReadXml(archive, "xl/worksheets/sheet5.xml");
                    Assert.Contains("SELECT OrderId, CustomerId FROM Order", views.ToString());
                    foreach (var entry in archive.Entries.Where(x => x.FullName.EndsWith(".xml", StringComparison.Ordinal) || x.FullName.EndsWith(".rels", StringComparison.Ordinal)))
                        ReadXml(archive, entry.FullName);
                }
            }

            using (var word = new MemoryStream())
            {
                exporter.WriteWord(model, word);
                word.Position = 0;
                using (var archive = new ZipArchive(word, ZipArchiveMode.Read, true))
                {
                    var document = ReadXml(archive, "word/document.xml");
                    Assert.Contains("Table comment &amp; notes.", document.ToString());
                    Assert.Contains("Order Summary", document.ToString());
                    XNamespace wordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                    var tableProperties = document.Descendants(wordNamespace + "tblPr").ToArray();
                    Assert.NotEmpty(tableProperties);
                    foreach (var properties in tableProperties)
                    {
                        var width = properties.Element(wordNamespace + "tblW");
                        Assert.Equal("5000", (string)width.Attribute(wordNamespace + "w"));
                        Assert.Equal("pct", (string)width.Attribute(wordNamespace + "type"));
                        var margins = properties.Element(wordNamespace + "tblCellMar");
                        Assert.Equal("80", (string)margins.Element(wordNamespace + "left").Attribute(wordNamespace + "w"));
                        Assert.Equal("80", (string)margins.Element(wordNamespace + "right").Attribute(wordNamespace + "w"));
                    }
                    Assert.NotNull(ReadXml(archive, "word/styles.xml"));
                    foreach (var entry in archive.Entries.Where(x => x.FullName.EndsWith(".xml", StringComparison.Ordinal) || x.FullName.EndsWith(".rels", StringComparison.Ordinal)))
                        ReadXml(archive, entry.FullName);
                }
            }
        }

        /// <summary>
        /// 验证自定义标签和默认标签可以共同使用。
        /// </summary>
        [Fact]
        public void ExportersAcceptCustomLabelsAndKeepBuiltInFallbacks()
        {
            var model = new PdmReader().ReadFromFile(Fixture);
            var labels = new Dictionary<string, string>
            {
                ["Tables"] = "Entity Catalogue & Tables",
                ["SheetColumns"] = "Properties",
                ["TableId"] = "Entity ID",
                ["Search"] = string.Empty
            };
            var provider = new DictionaryPdmExportLabelProvider(labels);

            var html = new StringWriter();
            new PdmExporter(PdmExportLanguage.Chinese, provider).Write(model, PdmExportFormat.Html, html);
            Assert.Contains("Entity Catalogue &amp; Tables", html.ToString());
            Assert.Contains("搜索", html.ToString());

            var templateHtml = new StringWriter();
            new PdmExporter(PdmExportLanguage.Chinese, provider, new[] { new FixtureHtmlTemplate() })
                .Write(model, PdmExportFormat.Html, templateHtml);
            Assert.Equal("custom-template:Entity Catalogue & Tables:2:搜索", templateHtml.ToString());

            using (var excel = new MemoryStream())
            {
                new PdmOfficeExporter(PdmExportLanguage.Chinese, provider).WriteExcel(model, excel);
                excel.Position = 0;
                using (var archive = new ZipArchive(excel, ZipArchiveMode.Read, true))
                {
                    XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    var workbook = ReadXml(archive, "xl/workbook.xml");
                    Assert.Contains(workbook.Root.Element(spreadsheet + "sheets").Elements(spreadsheet + "sheet"),
                        sheet => (string)sheet.Attribute("name") == "Entity Catalogue & Tables");
                    Assert.Contains(workbook.Root.Element(spreadsheet + "sheets").Elements(spreadsheet + "sheet"),
                        sheet => (string)sheet.Attribute("name") == "Properties");
                    Assert.Contains("Entity ID", ReadXml(archive, "xl/worksheets/sheet2.xml").ToString());
                }
            }
        }

        /// <summary>
        /// 验证厂商类型映射和精确覆盖规则。
        /// </summary>
        [Fact]
        public void MapsVendorTypesAndAllowsExactOverrides()
        {
            var mapper = new CSharpTypeMapper();
            Assert.Equal("short", mapper.GetCSharpType(new ColumnInfo { DataType = "int2", Mandatory = true }, "PGSQL", "PostgreSQL"));
            Assert.Equal("DateTimeOffset?", mapper.GetCSharpType(new ColumnInfo { DataType = "timestamp with time zone" }, "PGSQL", "PostgreSQL"));
            Assert.Equal("TimeSpan?", mapper.GetCSharpType(new ColumnInfo { DataType = "time without time zone" }, "PGSQL", "PostgreSQL"));
            Assert.Equal("byte[]", mapper.GetCSharpType(new ColumnInfo { DataType = "timestamp", Mandatory = true }, "MSSQLSRV", "Microsoft SQL Server"));
            Assert.Equal("bool", mapper.GetCSharpType(new ColumnInfo { DataType = "tinyint(1)", Mandatory = true }, "MYSQL", "MySQL"));
            Assert.Equal("uint?", mapper.GetCSharpType(new ColumnInfo { DataType = "int unsigned" }, "MYSQL", "MySQL"));
            Assert.Equal("uint?", mapper.GetCSharpType(new ColumnInfo { DataType = "int(11) unsigned" }, "MYSQL", "MySQL"));
            Assert.Equal("DateTimeOffset?", mapper.GetCSharpType(new ColumnInfo { DataType = "time(6) with time zone" }, "PGSQL", "PostgreSQL"));
            Assert.Equal("DateTime?", mapper.GetCSharpType(new ColumnInfo { DataType = "DATE" }, "ORACLE", "Oracle"));
            Assert.Equal("byte[]", mapper.GetCSharpType(new ColumnInfo { DataType = "RAW(16)", Mandatory = true }, "ORACLE", "Oracle"));
            Assert.Equal("decimal", mapper.GetCSharpType(new ColumnInfo { DataType = "NUMBER(12,2)", Mandatory = true }, "ORACLE", "Oracle"));

            mapper.RegisterMapping("geography", "Microsoft.SqlServer.Types.SqlGeography", "MSSQLSRV");
            mapper.RegisterMapping("geography", "My.GlobalGeography");
            Assert.Equal("Microsoft.SqlServer.Types.SqlGeography",
                mapper.GetCSharpType(new ColumnInfo { DataType = "Geography" }, "MSSQLSRV", "Microsoft SQL Server"));
            Assert.Equal("My.GlobalGeography",
                mapper.GetCSharpType(new ColumnInfo { DataType = "Geography", Mandatory = true }, "OTHER", "Other DBMS"));
            Assert.Throws<ArgumentException>(() => mapper.RegisterMapping("geography", "bad type", "MSSQLSRV"));
            var unsupported = Assert.Throws<InvalidOperationException>(() => mapper.GetCSharpType(
                new ColumnInfo { Code = "UnknownValue", DataType = "mysterytype" }, "OTHER", "Other DBMS"));
            Assert.Contains("Other DBMS", unsupported.Message);
            Assert.Contains("UnknownValue", unsupported.Message);
        }

        /// <summary>
        /// 验证自定义值类型映射与兜底类型的可空性和类型校验。
        /// </summary>
        [Fact]
        public void CustomValueTypesPreserveNullableColumnsAndRejectInvalidTypes()
        {
            var mapper = new CSharpTypeMapper();
            mapper.RegisterFallback("int");
            mapper.RegisterMapping("special", "Guid");
            Assert.Equal("int?", mapper.Resolve(new ColumnInfo { DataType = "unknown" }, "MSSQLSRV", "SQL Server").TypeName);
            Assert.Equal("Guid?", mapper.GetCSharpType(new ColumnInfo { DataType = "special" }, "MSSQLSRV", "SQL Server"));
            Assert.Equal("int", mapper.GetCSharpType(new ColumnInfo { DataType = "int", Mandatory = true }, "MSSQLSRV", "SQL Server"));
            Assert.Throws<ArgumentException>(() => mapper.RegisterFallback("void"));
            Assert.Throws<ArgumentException>(() => mapper.RegisterMapping("special", "System.Void"));
            Assert.Throws<ArgumentException>(() => mapper.RegisterMapping("special", "class"));
            Assert.Throws<ArgumentException>(() => mapper.RegisterFallback("System.class"));
        }

        /// <summary>
        /// 验证自定义实体模板可使用已解析属性。
        /// </summary>
        [Fact]
        public void EntityGeneratorAcceptsCustomTemplatesWithResolvedProperties()
        {
            var table = new Bing.Pdm.Models.Tables.TableInfo { Code = "sample" };
            table.Columns.Add(new ColumnInfo { Code = "value", DataType = "int", Mandatory = true });
            table.Columns.Add(new ColumnInfo { Code = "Value", DataType = "int" });
            var output = Path.Combine(Path.GetTempPath(), "PdmTemplate-" + Guid.NewGuid().ToString("N"));
            var template = new FixtureCSharpTemplate();
            Directory.CreateDirectory(output);
            try
            {
                EntityGenerator.Generate(new[] { table }, output, "Demo.Entities", "MSSQLSRV", "Microsoft SQL Server",
                    new CSharpTypeMapper(), template);
                Assert.Equal("Demo.Entities|Sample|Value:int:value,Value_2:int?:Value|sample",
                    File.ReadAllText(Path.Combine(output, "Sample.cs")));
                Assert.Same(table, template.Table);
                Assert.Same(table.Columns[0], template.FirstColumn);
                Assert.Equal("sample", table.Code);
                Assert.Equal("value", table.Columns[0].Code);
            }
            finally
            {
                if (Directory.Exists(output)) Directory.Delete(output, true);
            }
        }

        /// <summary>
        /// 验证同名实体不会遮蔽生成属性使用的系统类型。
        /// </summary>
        [Fact]
        public void DefaultEntityTemplateQualifiesFrameworkTypes()
        {
            var dateTime = new TableInfo { Code = "DateTime" };
            dateTime.Columns.Add(new ColumnInfo { Code = "Created", DataType = "datetime", Mandatory = true });
            var order = new TableInfo { Code = "Order" };
            order.Columns.Add(new ColumnInfo { Code = "Created", DataType = "datetime", Mandatory = true });
            var output = Path.Combine(Path.GetTempPath(), "pdm-type-name-" + Guid.NewGuid().ToString("N"));
            try
            {
                EntityGenerator.Generate(new[] { dateTime, order }, output, "Generated.Entities", "MSSQLSRV", "SQL Server");
                Assert.Contains("global::System.DateTime Created", File.ReadAllText(Path.Combine(output, "DateTime.cs")));
                Assert.Contains("global::System.DateTime Created", File.ReadAllText(Path.Combine(output, "Order.cs")));
                var project = Path.Combine(output, "Generated.csproj");
                File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
                Assert.Equal(0, RunDotnet(output, "build", project, "--nologo", "--verbosity", "quiet").ExitCode);
            }
            finally
            {
                if (Directory.Exists(output)) Directory.Delete(output, true);
            }
        }

        /// <summary>
        /// 验证命令行可导出全部格式并编译生成实体。
        /// </summary>
        [Fact]
        public void CliExportsAllFormatsAndGeneratedEntitiesCompile()
        {
            var root = FindRepositoryRoot();
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
            var tool = Path.Combine(root, "samples", "Bing.Pdm.Tool", "bin", configuration, "net8.0", "Bing.Pdm.Tool.dll");
            Assert.True(File.Exists(tool), "CLI assembly was not built: " + tool);
            var work = Path.Combine(Path.GetTempPath(), "Bing.Pdm.Tests-" + Guid.NewGuid().ToString("N"));
            var exports = Path.Combine(work, "exports");
            var entities = Path.Combine(work, "entities");
            Directory.CreateDirectory(work);
            try
            {
                var export = RunDotnet(work, tool, "export", Fixture, exports, "json,md,html,svg,xlsx,docx", "zh");
                Assert.Equal(0, export.ExitCode);
                Assert.True(File.Exists(Path.Combine(exports, "complete.json")));
                Assert.True(File.Exists(Path.Combine(exports, "complete.md")));
                Assert.True(File.Exists(Path.Combine(exports, "complete.html")));
                Assert.True(File.Exists(Path.Combine(exports, "complete-Overview.svg")));
                Assert.True(File.Exists(Path.Combine(exports, "complete.xlsx")));
                Assert.True(File.Exists(Path.Combine(exports, "complete.docx")));
                Assert.Contains("数据表", File.ReadAllText(Path.Combine(exports, "complete.html")));

                var invalidFormats = Path.Combine(work, "invalid-formats");
                var invalidExport = RunDotnet(work, tool, "export", Fixture, invalidFormats, "json,not-a-format", "en");
                Assert.Equal(1, invalidExport.ExitCode);
                Assert.Contains("Unknown export format", invalidExport.StandardError);
                Assert.False(Directory.Exists(invalidFormats));

                var noDiagramDocument = XDocument.Load(Fixture);
                noDiagramDocument.Descendants(XName.Get("PhysicalDiagrams", "collection")).Remove();
                var noDiagramInput = Path.Combine(work, "no-diagram.pdm");
                noDiagramDocument.Save(noDiagramInput);
                var noDiagramOutput = Path.Combine(work, "no-diagram");
                var noDiagramExport = RunDotnet(work, tool, "export", noDiagramInput, noDiagramOutput, "json,svg", "en");
                Assert.Equal(1, noDiagramExport.ExitCode);
                Assert.Contains("no physical diagrams", noDiagramExport.StandardError);
                Assert.False(Directory.Exists(noDiagramOutput));

                var duplicateDiagramInput = Path.Combine(work, "duplicate-diagrams.pdm");
                var duplicateDiagramXml = File.ReadAllText(Fixture).Replace(
                    "</c:PhysicalDiagrams>",
                    "<o:PhysicalDiagram Id=\"o25\"><a:ObjectID>00000000-0000-0000-0000-000000000025</a:ObjectID><a:Name>Overview</a:Name><a:Code>Overview</a:Code><c:Symbols/></o:PhysicalDiagram></c:PhysicalDiagrams>");
                File.WriteAllText(duplicateDiagramInput, duplicateDiagramXml);
                var duplicateDiagramOutput = Path.Combine(work, "duplicate-diagrams");
                var duplicateExport = RunDotnet(work, tool, "export", duplicateDiagramInput, duplicateDiagramOutput, "svg", "en");
                Assert.Equal(0, duplicateExport.ExitCode);
                Assert.True(File.Exists(Path.Combine(duplicateDiagramOutput, "duplicate-diagrams-Overview.svg")));
                Assert.True(File.Exists(Path.Combine(duplicateDiagramOutput, "duplicate-diagrams-Overview-2.svg")));
                Assert.Equal(2, Directory.GetFiles(duplicateDiagramOutput, "*.svg").Length);

                var generated = RunDotnet(work, tool, "generate", Fixture, entities, "Generated.Fixture");
                Assert.Equal(0, generated.ExitCode);
                Assert.Contains("class Order", File.ReadAllText(Path.Combine(entities, "Order.cs")));
                Assert.Contains("byte[] RowVersion", File.ReadAllText(Path.Combine(entities, "Order.cs")));
                var project = Path.Combine(entities, "Generated.csproj");
                File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup></Project>");
                var build = RunDotnet(work, "build", project, "--nologo", "--verbosity", "quiet");
                Assert.Equal(0, build.ExitCode);

                var invalidNamespace = RunDotnet(work, tool, "generate", Fixture, Path.Combine(work, "invalid"), "Generated.class");
                Assert.Equal(1, invalidNamespace.ExitCode);
                Assert.Contains("Invalid C# namespace", invalidNamespace.StandardError);

                var postgresInput = Path.Combine(work, "postgres.pdm");
                var postgresXml = File.ReadAllText(Fixture).Replace("Microsoft SQL Server", "PostgreSQL").Replace("MSSQLSRV", "PGSQL");
                File.WriteAllText(postgresInput, postgresXml);
                var postgresEntities = Path.Combine(work, "postgres-entities");
                var postgresGenerate = RunDotnet(work, tool, "generate", postgresInput, postgresEntities, "Generated.Postgres");
                Assert.Equal(0, postgresGenerate.ExitCode);
                Assert.Contains("DateTime? RowVersion", File.ReadAllText(Path.Combine(postgresEntities, "Order.cs")));

                var customTypeInput = Path.Combine(work, "custom-types.pdm");
                File.WriteAllText(customTypeInput, File.ReadAllText(Fixture).Replace("<a:DataType>nvarchar</a:DataType>", "<a:DataType>geography</a:DataType>"));
                var customTypeEntities = Path.Combine(work, "custom-types");
                var customTypeGenerate = RunDotnet(work, tool, "generate", customTypeInput, customTypeEntities,
                    "Generated.Custom", "--map", "MSSQLSRV:geography=string");
                Assert.Equal(0, customTypeGenerate.ExitCode);
                Assert.Contains("public string DisplayNote", File.ReadAllText(Path.Combine(customTypeEntities, "Order.cs")));
            }
            finally
            {
                if (Directory.Exists(work)) Directory.Delete(work, true);
            }
        }

        /// <summary>
        /// 读取 Office 压缩包中的 XML 部件。
        /// </summary>
        /// <param name="archive">包含 XML 部件的 Office 文档压缩包。</param>
        /// <param name="entryName">压缩包内的部件路径。</param>
        /// <returns>解析后的 XML 文档。</returns>
        private static XDocument ReadXml(ZipArchive archive, string entryName)
        {
            var entry = archive.GetEntry(entryName);
            Assert.NotNull(entry);
            using (var stream = entry.Open()) return XDocument.Load(stream);
        }

        /// <summary>
        /// 递归枚举 JSON 中的表对象。
        /// </summary>
        /// <param name="node">当前 JSON 对象或数组节点。</param>
        /// <returns>当前节点及其嵌套包中的表对象。</returns>
        private static IEnumerable<JToken> JsonTables(JToken node)
        {
            var objectNode = node as JObject;
            if (objectNode == null) yield break;

            var tables = objectNode["Tables"] as JArray;
            if (tables != null)
                foreach (var table in tables)
                    yield return table;

            var packages = objectNode["Packages"] as JArray;
            if (packages == null) yield break;
            foreach (var package in packages)
                foreach (var table in JsonTables(package))
                    yield return table;
        }

        /// <summary>
        /// 提取 Markdown 中指定表的章节。
        /// </summary>
        /// <param name="model">包含表及包层级的 PDM 模型。</param>
        /// <param name="markdown">待搜索的 Markdown 文本。</param>
        /// <param name="table">目标表。</param>
        /// <returns>目标表标题开始至下一表标题前的章节文本。</returns>
        private static string ExtractMarkdownTableSection(PdmInfo model, string markdown, TableInfo table)
        {
            var marker = "\n## " + MarkdownTablePath(model, table) + table.Code + " ";
            var start = markdown.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(start >= 0, "Markdown table heading was not found for " + table.Id + ".");
            var end = markdown.IndexOf("\n## ", start + marker.Length, StringComparison.Ordinal);
            return markdown.Substring(start, end < 0 ? markdown.Length - start : end - start);
        }

        /// <summary>
        /// 获取表在 Markdown 中使用的包路径。
        /// </summary>
        /// <param name="model">包含包层级的 PDM 模型。</param>
        /// <param name="table">待查找路径的表。</param>
        /// <returns>表所在包路径及分隔符；顶层表返回空字符串。</returns>
        private static string MarkdownTablePath(PdmInfo model, TableInfo table)
        {
            foreach (var package in model.Packages)
            {
                var path = FindPackagePath(package, table);
                if (path != null) return path + " / ";
            }
            return string.Empty;
        }

        /// <summary>
        /// 递归查找表所属包路径。
        /// </summary>
        /// <param name="package">当前待搜索的包。</param>
        /// <param name="table">目标表。</param>
        /// <returns>表所在的相对包路径；未找到时返回 <see langword="null"/>。</returns>
        private static string FindPackagePath(PackageInfo package, TableInfo table)
        {
            var packageName = package.Name ?? package.Code ?? package.Id;
            if (package.Tables.Contains(table)) return packageName;
            foreach (var child in package.Packages)
            {
                var childPath = FindPackagePath(child, table);
                if (childPath != null) return packageName + " / " + childPath;
            }
            return null;
        }

        /// <summary>
        /// 统计 Markdown 表章节中的列行数。
        /// </summary>
        /// <param name="section">Markdown 表章节文本。</param>
        /// <returns>章节中的数据行数量。</returns>
        private static int CountMarkdownColumnRows(string section)
        {
            return section.Replace("\r\n", "\n").Split('\n')
                .Count(line => line.StartsWith("| ", StringComparison.Ordinal)
                    && !line.StartsWith("| Column ", StringComparison.Ordinal)
                    && !line.StartsWith("| --- ", StringComparison.Ordinal));
        }

        /// <summary>
        /// 提取 HTML 中指定表的文章内容。
        /// </summary>
        /// <param name="html">待搜索的 HTML 文本。</param>
        /// <param name="tableId">目标表标识。</param>
        /// <returns>目标表对应的 article 元素文本。</returns>
        private static string ExtractHtmlTableArticle(string html, string tableId)
        {
            var marker = "<article id=\"" + HtmlTableId(tableId) + "\"";
            var start = html.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(start >= 0, "HTML table article was not found for " + tableId + ".");
            var end = html.IndexOf("</article>", start, StringComparison.Ordinal);
            Assert.True(end >= 0, "HTML table article was not closed for " + tableId + ".");
            return html.Substring(start, end - start);
        }

        /// <summary>
        /// 生成与导出器一致的 HTML 表锚点。
        /// </summary>
        /// <param name="tableId">表标识。</param>
        /// <returns>表对应的 HTML 锚点标识。</returns>
        private static string HtmlTableId(string tableId)
        {
            return "table-" + Convert.ToBase64String(Encoding.UTF8.GetBytes(tableId ?? string.Empty))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// 统计 HTML 表格中的列行数。
        /// </summary>
        /// <param name="article">包含表格的 HTML article 文本。</param>
        /// <returns>表格中的数据行数量。</returns>
        private static int CountHtmlColumnRows(string article)
        {
            return article.Split(new[] { "<tr><td" }, StringSplitOptions.None).Length - 1;
        }

        /// <summary>
        /// 查找仓库根目录。
        /// </summary>
        /// <returns>包含 solution 文件的仓库根目录路径。</returns>
        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Bing.Pdm.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate Bing.Pdm.sln from the test output directory.");
        }

        /// <summary>
        /// 在指定目录执行 dotnet 命令。
        /// </summary>
        /// <param name="workingDirectory">命令工作目录。</param>
        /// <param name="arguments">传递给 dotnet 的命令行参数。</param>
        /// <returns>进程退出代码、标准输出和标准错误。</returns>
        private static ProcessResult RunDotnet(string workingDirectory, params string[] arguments)
        {
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using (var process = Process.Start(start))
            {
                Assert.NotNull(process);
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ProcessResult(process.ExitCode, stdout, stderr);
            }
        }

        /// <summary>
        /// 保存外部进程的退出代码和标准输出。
        /// </summary>
        private sealed class ProcessResult
        {
            /// <summary>
            /// 获取进程退出代码。
            /// </summary>
            public int ExitCode { get; }
            /// <summary>
            /// 获取标准输出文本。
            /// </summary>
            public string StandardOutput { get; }
            /// <summary>
            /// 获取标准错误文本。
            /// </summary>
            public string StandardError { get; }

            /// <summary>
            /// 初始化一个 <see cref="ProcessResult"/> 类型的实例。
            /// </summary>
            /// <param name="exitCode">进程退出代码。</param>
            /// <param name="standardOutput">标准输出文本。</param>
            /// <param name="standardError">标准错误文本。</param>
            public ProcessResult(int exitCode, string standardOutput, string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput;
                StandardError = standardError;
            }
        }

        /// <summary>
        /// 用于验证的自定义 HTML 模板。
        /// </summary>
        private sealed class FixtureHtmlTemplate : IPdmExportTemplate
        {
            /// <inheritdoc />
            public PdmExportFormat Format => PdmExportFormat.Html;

            /// <inheritdoc />
            public void Write(Bing.Pdm.Models.PdmInfo model, IPdmExportLabelProvider labels, TextWriter writer)
            {
                writer.Write($"custom-template:{labels.GetLabel("Tables")}:{model.AllTables.Count()}:{labels.GetLabel("Search")}");
            }
        }

        /// <summary>
        /// 用于验证的自定义 C# 实体模板。
        /// </summary>
        private sealed class FixtureCSharpTemplate : ICSharpEntityTemplate
        {
            /// <summary>
            /// 获取模板收到的源表。
            /// </summary>
            public Bing.Pdm.Models.Tables.TableInfo Table { get; private set; }
            /// <summary>
            /// 获取模板收到的首个列。
            /// </summary>
            public ColumnInfo FirstColumn { get; private set; }

            /// <inheritdoc />
            public string Render(CSharpEntityTemplateContext context)
            {
                Table = context.Table;
                FirstColumn = context.Properties[0].Column;
                var properties = string.Join(",", context.Properties.Select(property =>
                    property.Name + ":" + property.ClrType + ":" + property.Column.Code));
                return context.Namespace + "|" + context.EntityName + "|" + properties + "|" + context.Table.Code;
            }
        }
    }
}
