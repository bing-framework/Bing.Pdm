using System;
using System.IO;
using System.Linq;
using System.Text;
using Bing.Pdm;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Reader;
using Bing.Pdm.Tool;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证真实 PDM 结构兼容性。
    /// </summary>
    public sealed class RealPdmCompatibilityTests
    {
        /// <summary>
        /// 获取 Shortcut 和索引 fixture 的路径。
        /// </summary>
        private static string Fixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "real-compat.pdm");
        /// <summary>
        /// 获取扩展图形符号 fixture 的路径。
        /// </summary>
        private static string SymbolsFixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "symbols-compat.pdm");

        /// <summary>
        /// 验证 Shortcut、引用和索引列变体的解析。
        /// </summary>
        [Fact]
        public void ResolvesShortcutsRelationsAndIndexColumnVariant()
        {
            var model = new PdmReader().ReadFromFile(Fixture);

            Assert.Equal("MSSQLSRV2012", model.DbmsCode);
            Assert.True(model.Lookup.TryGetShortcut("shortcut-target", out var shortcut));
            Assert.Equal("target-guid", shortcut.TargetId);
            Assert.Equal("target-table", shortcut.ResolvedTargetId);
            Assert.Equal("Table", shortcut.TargetKind);
            Assert.True(model.Lookup.TryGetTable("target-table", out var target));

            var reference = Assert.Single(model.AllReferences);
            Assert.True(model.Lookup.TryGetTable(reference.ParentTableId, out var parent));
            Assert.Same(target, parent);
            Assert.Equal("shortcut-target", reference.RawParentTableRef);
            Assert.Equal("target-table", reference.ParentTableId);
            Assert.Equal("child-table", reference.ChildTableId);
            Assert.Single(reference.Joins);
            Assert.Equal("target-id", reference.Joins[0].ParentColumnId);
            Assert.Equal("child-id", reference.Joins[0].ChildColumnId);

            var child = model.AllTables.Single(table => table.Id == "child-table");
            var index = Assert.Single(child.Indexes);
            Assert.Contains("child-id", index.ColumnIds);
            Assert.Equal("index-column1", Assert.Single(index.IndexColumns).Id);
            Assert.Equal("child-id", index.IndexColumns[0].ColumnId);
            Assert.DoesNotContain(model.Diagnostics, diagnostic => diagnostic.Code == "UNRESOLVED_REF");
        }

        /// <summary>
        /// 验证嵌套符号和物理图表现数据。
        /// </summary>
        [Fact]
        public void FlattensNestedSymbolsAndPreservesDiagramPresentationData()
        {
            var model = new PdmReader().ReadFromFile(Fixture);
            var diagram = Assert.Single(model.AllPhysicalDiagrams);
            var nested = diagram.AllSymbols.Single(symbol => symbol.Id == "nested-shortcut-symbol1");
            Assert.Equal("target-table", nested.ObjectId);
            Assert.Equal("shortcut-target", nested.RawObjectRef);
            Assert.Equal(100, nested.Rect.X1);
            Assert.Equal("#00ff00", nested.LineColor);
            Assert.Equal("#00ffff", nested.FillColor);

            var package = diagram.Symbols.Single(symbol => symbol.Id == "package-symbol1");
            Assert.Equal(2, package.SubSymbols.Count);

            var note = diagram.AllSymbols.Single(symbol => symbol.Id == "note-symbol1");
            Assert.Equal("你好", note.Text);
            using var writer = new StringWriter();
            new PdmExporter().WriteDiagram(model, diagram, writer);
            var svg = writer.ToString();
            Assert.Contains("你好", svg);
            Assert.Contains("#00ff00", svg);
            Assert.Contains("data-symbol-id=\"nested-shortcut-symbol1\"", svg);
            Assert.Contains("1540,640 1940,640", svg);
        }

        /// <summary>
        /// 验证 PDM 命名空间前缀可以变化。
        /// </summary>
        [Fact]
        public void NamespacePrefixesAreNotPartOfThePdmContract()
        {
            var xml = File.ReadAllText(Fixture);
            var prefixed = xml.Replace("pdma", "altA").Replace("pdmc", "altC").Replace("pdmo", "altO");

            var model = new PdmReader().Read(new MemoryStream(Encoding.UTF8.GetBytes(prefixed)));
            Assert.Equal("RealCompat", model.Code);
            Assert.Equal(2, model.AllTables.Count());
            Assert.Single(model.AllReferences);
            Assert.Single(model.AllPhysicalDiagrams);
        }

        /// <summary>
        /// 验证未解析 Shortcut 目标保留为未解析状态。
        /// </summary>
        [Fact]
        public void ShortcutTargetFailuresRemainUnresolved()
        {
            var xml = File.ReadAllText(Fixture);
            var missing = new PdmReader().Read(new MemoryStream(Encoding.UTF8.GetBytes(
                xml.Replace("<pdma:TargetID>target-guid</pdma:TargetID>", "<pdma:TargetID>absent-guid</pdma:TargetID>"))));
            Assert.Contains(missing.Diagnostics, x => x.Code == "UNRESOLVED_SHORTCUT_TARGET" && x.SourceId == "shortcut-target");
            Assert.Null(Assert.Single(missing.AllShortcuts).ResolvedTargetId);

            var ambiguous = new PdmReader().Read(new MemoryStream(Encoding.UTF8.GetBytes(
                xml.Replace("<pdma:ObjectID>child-guid</pdma:ObjectID>", "<pdma:ObjectID>target-guid</pdma:ObjectID>"))));
            Assert.Contains(ambiguous.Diagnostics, x => x.Code == "AMBIGUOUS_SHORTCUT_TARGET" && x.SourceId == "shortcut-target");
            Assert.Null(Assert.Single(ambiguous.AllShortcuts).ResolvedTargetId);
        }

        /// <summary>
        /// 验证索引列必须属于当前表。
        /// </summary>
        [Fact]
        public void IndexColumnMustBelongToItsTable()
        {
            var xml = File.ReadAllText(Fixture).Replace(
                "<pdmc:Column><pdmo:Column Ref=\"child-id\"/></pdmc:Column>",
                "<pdmc:Column><pdmo:Column Ref=\"target-id\"/></pdmc:Column>");
            var model = new PdmReader().Read(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
            Assert.Contains(model.Diagnostics, x => x.Code == "UNRESOLVED_REF" && x.SourceId == "index1");
        }

        /// <summary>
        /// 验证生成预检一次报告全部类型错误且不创建文件。
        /// </summary>
        [Fact]
        public void GenerationPreflightReportsEveryBadTypeWithoutCreatingFiles()
        {
            var table = new TableInfo { Code = "BadTypes" };
            table.Columns.Add(new ColumnInfo { Code = "Empty", DataType = "" });
            table.Columns.Add(new ColumnInfo { Code = "Typo", DataType = "interger" });
            var output = Path.Combine(Path.GetTempPath(), "pdm-preflight-" + Guid.NewGuid().ToString("N"));
            var result = EntityGenerator.GenerateWithDiagnostics(new[] { table }, output, "Demo.Entities", "MSSQLSRV", "SQL Server");
            Assert.Equal(2, result.Diagnostics.Count);
            Assert.All(result.Diagnostics, item => Assert.Equal("TYPE_ERROR", item.Code));
            Assert.False(Directory.Exists(output));
        }

        /// <summary>
        /// 验证生成预检不会修改已有输出目录。
        /// </summary>
        [Fact]
        public void GenerationPreflightDoesNotModifyAnExistingOutputDirectory()
        {
            var table = new TableInfo { Code = "BadTypes" };
            table.Columns.Add(new ColumnInfo { Code = "Empty", DataType = "" });
            var output = Path.Combine(Path.GetTempPath(), "pdm-existing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            var sentinel = Path.Combine(output, "keep.txt");
            File.WriteAllText(sentinel, "existing");
            try
            {
                var result = EntityGenerator.GenerateWithDiagnostics(new[] { table }, output, "Demo.Entities", "MSSQLSRV", "SQL Server");
                Assert.Contains(result.Diagnostics, item => item.Code == "TYPE_ERROR");
                Assert.Equal("existing", File.ReadAllText(sentinel));
                Assert.False(File.Exists(Path.Combine(output, "BadTypes.cs")));
            }
            finally
            {
                if (Directory.Exists(output)) Directory.Delete(output, true);
            }
        }

        /// <summary>
        /// 验证显式兜底类型和精确映射的优先级。
        /// </summary>
        [Fact]
        public void ExplicitFallbackReportsEveryAffectedColumnAndMappingWins()
        {
            var table = new TableInfo { Code = "BadTypes" };
            table.Columns.Add(new ColumnInfo { Code = "Empty", DataType = "" });
            table.Columns.Add(new ColumnInfo { Code = "Typo", DataType = "interger" });
            var mapper = new CSharpTypeMapper();
            mapper.RegisterFallback("object");
            mapper.RegisterMapping("interger", "long", "MSSQLSRV");
            var output = Path.Combine(Path.GetTempPath(), "pdm-fallback-" + Guid.NewGuid().ToString("N"));
            try
            {
                var result = EntityGenerator.GenerateWithDiagnostics(new[] { table }, output, "Demo.Entities", "MSSQLSRV", "SQL Server", mapper);
                Assert.Equal(1, result.FileCount);
                Assert.Equal("Empty", Assert.Single(result.Diagnostics).Column);
                Assert.Equal("TYPE_FALLBACK", result.Diagnostics[0].Code);
                var source = File.ReadAllText(Path.Combine(output, "BadTypes.cs"));
                Assert.Contains("object Empty", source);
                Assert.Contains("long Typo", source);
            }
            finally
            {
                if (Directory.Exists(output)) Directory.Delete(output, true);
            }
        }

        /// <summary>
        /// 验证全局映射优先于厂商内置映射。
        /// </summary>
        [Fact]
        public void GlobalMappingsPrecedeVendorBuiltIns()
        {
            var mapper = new CSharpTypeMapper();
            mapper.RegisterMapping("int", "long");
            mapper.RegisterMapping("timestamp", "string");
            Assert.Equal("long", mapper.GetCSharpType(new ColumnInfo { DataType = "int", Mandatory = true }, "MYSQL", "MySQL"));
            Assert.Equal("string", mapper.GetCSharpType(new ColumnInfo { DataType = "timestamp", Mandatory = true }, "MSSQLSRV", "SQL Server"));
        }

        /// <summary>
        /// 验证扩展符号离线渲染和未知几何诊断。
        /// </summary>
        [Fact]
        public void ExtendedSymbolsRenderOfflineAndUnknownGeometryIsDiagnosed()
        {
            var model = new PdmReader().ReadFromFile(SymbolsFixture);
            var diagram = Assert.Single(model.AllPhysicalDiagrams);
            Assert.Equal(8, diagram.AllSymbols.Count());
            Assert.Contains(model.Diagnostics, x => x.Code == "UNSUPPORTED_DIAGRAM_SYMBOL" && x.SourceId == "unknown");
            Assert.DoesNotContain(model.Diagnostics, x => x.Code == "UNRESOLVED_REF");
            Assert.Equal("你好 &", diagram.AllSymbols.Single(x => x.Id == "note").Text);
            Assert.Equal("#ff0000", diagram.AllSymbols.Single(x => x.Id == "ellipse").LineColor);
            Assert.Equal("20", diagram.AllSymbols.Single(x => x.Id == "predefined").SymbolType);
            using var writer = new StringWriter();
            new PdmExporter().Write(model, PdmExportFormat.Html, writer);
            var html = writer.ToString();
            Assert.Contains("data-symbol-id=\"ellipse\"", html);
            Assert.Contains("data-symbol-id=\"note-link\"", html);
            Assert.Contains("data-symbol-id=\"dependency\"", html);
            Assert.Contains("Text &amp; &lt;shape&gt;", html);
            Assert.Contains("data-zoom=\"in\"", html);
            Assert.DoesNotContain("src=\"http", html);
        }

    }
}
