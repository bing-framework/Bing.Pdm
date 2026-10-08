using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Bing.Pdm;
using Bing.Pdm.Models;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Models.Views;
using Bing.Pdm.Reader;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证导出 JSON 的离线读取和再次使用。
    /// </summary>
    public sealed class PdmJsonReaderTests
    {
        /// <summary>
        /// 获取真实结构 fixture 路径。
        /// </summary>
        private static string CompatibilityFixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "real-compat.pdm");

        /// <summary>
        /// 获取完整模型 fixture 路径。
        /// </summary>
        private static string CompleteFixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "complete.pdm");

        /// <summary>
        /// 验证 JSON 往返保留对象关系和可重建的索引。
        /// </summary>
        [Fact]
        public void RoundTripPreservesHierarchyReferencesAndLookup()
        {
            var source = new PdmReader().ReadFromFile(CompatibilityFixture);
            var nested = new PackageInfo { Id = "nested-package", Code = "Nested" };
            nested.Tables.Add(new TableInfo { Id = "nested-table", Code = "NestedTable", PackageId = nested.Id });
            source.Packages[0].Packages.Add(nested);
            var view = new ViewInfo { Id = "extra-view", Code = "ExtraView" };
            view.Columns.Add(new ViewColumnInfo { Id = "extra-view-column", Code = "CreatedAt" });
            source.Views.Add(view);
            source.Diagnostics.Add(new PdmDiagnostic { Code = "TEST", SourceId = "nested-table", Message = "Retained" });
            using var writer = new StringWriter();
            new PdmExporter().Write(source, PdmExportFormat.Json, writer);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString()));
            var restored = new PdmJsonReader().Read(stream);

            Assert.True(stream.CanRead);
            Assert.Equal(source.Id, restored.Id);
            Assert.Equal(source.AllTables.Count(), restored.AllTables.Count());
            Assert.Equal("Nested", restored.Packages[0].Packages[0].Code);
            Assert.True(restored.Lookup.TryGetPackage("nested-package", out _));
            Assert.True(restored.Lookup.TryGetTable("nested-table", out _));
            Assert.True(restored.Lookup.TryGetColumn("child-id", out _));
            Assert.True(restored.Lookup.TryGetKey("target-key", out _));
            Assert.True(restored.Lookup.TryGetDiagram("diagram1", out _));
            Assert.True(restored.Lookup.TryGetView("extra-view", out var restoredView));
            Assert.Equal("ExtraView", restoredView.Code);
            Assert.True(restored.Lookup.TryGetViewColumn("extra-view-column", out _));
            Assert.True(restored.Lookup.TryGetShortcut("shortcut-target", out var shortcut));
            Assert.Equal("target-guid", shortcut.TargetId);
            Assert.Equal("target-table", shortcut.ResolvedTargetId);
            Assert.True(restored.Lookup.TryGetReference("reference1", out var reference));
            Assert.Equal("shortcut-target", reference.RawParentTableRef);
            Assert.Equal("target-table", reference.ParentTableId);
            var sourceIndex = source.AllTables.Single(x => x.Id == "child-table").Indexes.Single();
            var restoredIndex = restored.AllTables.Single(x => x.Id == "child-table").Indexes.Single();
            Assert.Equal(sourceIndex.Unique, restoredIndex.Unique);
            Assert.Equal(sourceIndex.ColumnIds, restoredIndex.ColumnIds);
            Assert.Equal(sourceIndex.IndexColumns.Select(x => (x.Id, x.ColumnId)),
                restoredIndex.IndexColumns.Select(x => (x.Id, x.ColumnId)));
            Assert.True(restored.Lookup.TryGetSymbol("nested-shortcut-symbol1", out var symbol));
            Assert.Equal("shortcut-target", symbol.RawObjectRef);
            var sourceSymbol = source.AllPhysicalDiagrams.Single().AllSymbols.Single(x => x.Id == symbol.Id);
            Assert.Equal((sourceSymbol.Rect.X1, sourceSymbol.Rect.Y1, sourceSymbol.Rect.X2, sourceSymbol.Rect.Y2),
                (symbol.Rect.X1, symbol.Rect.Y1, symbol.Rect.X2, symbol.Rect.Y2));
            Assert.Equal(sourceSymbol.LineColor, symbol.LineColor);
            Assert.Equal(sourceSymbol.FillColor, symbol.FillColor);
            var sourceLine = source.AllPhysicalDiagrams.Single().AllSymbols.Single(x => x.Id == "reference-symbol1");
            var restoredLine = restored.AllPhysicalDiagrams.Single().AllSymbols.Single(x => x.Id == sourceLine.Id);
            Assert.Equal(sourceLine.Points.Select(x => (x.X, x.Y)), restoredLine.Points.Select(x => (x.X, x.Y)));
            Assert.Equal(source.Diagnostics.Select(x => (x.Code, x.SourceId, x.Message)),
                restored.Diagnostics.Select(x => (x.Code, x.SourceId, x.Message)));

            restored.Packages[0].Packages[0].Tables.Clear();
            restored.Tables.Add(new TableInfo { Id = "added-table", Code = "Added" });
            var count = restored.Diagnostics.Count;
            restored.RebuildLookup();
            Assert.False(restored.Lookup.TryGetTable("nested-table", out _));
            Assert.True(restored.Lookup.TryGetTable("added-table", out _));
            Assert.Equal(count, restored.Diagnostics.Count);
        }

        /// <summary>
        /// 验证复合主键和可选集合缺失时的读取行为。
        /// </summary>
        [Fact]
        public void ReadsCompositeKeyAndMissingOptionalCollections()
        {
            var source = new PdmReader().ReadFromFile(CompleteFixture);
            using var writer = new StringWriter();
            new PdmExporter().Write(source, PdmExportFormat.Json, writer);
            var restored = new PdmJsonReader().Read(new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString())));
            Assert.Contains(restored.AllTables.SelectMany(x => x.Keys), key => key.ColumnIds.Count == 2);
            Assert.True(restored.Lookup.TryGetKey("o54", out var key));
            Assert.Equal(2, key.ColumnIds.Count);
            Assert.True(restored.Lookup.TryGetOwner("o80", out _));
            Assert.True(restored.Lookup.TryGetView("o70", out var completeView));
            Assert.Equal("OrderSummary", completeView.Code);
            Assert.True(restored.Lookup.TryGetViewColumn("o71", out _));
            var customerIndex = restored.AllTables.Single(x => x.Id == "o50").Indexes.Single();
            Assert.True(customerIndex.Unique);
            Assert.Equal(new[] { "o53" }, customerIndex.ColumnIds);

            using var minimal = new MemoryStream(Encoding.UTF8.GetBytes("{\"Id\":\"minimal\",\"Tables\":[]}"));
            var empty = new PdmJsonReader().Read(minimal);
            Assert.Empty(empty.Packages);
            Assert.Empty(empty.References);
            Assert.Empty(empty.Diagnostics);
            Assert.NotNull(empty.Lookup);

            using var typed = new MemoryStream(Encoding.UTF8.GetBytes("{\"$type\":\"System.IO.FileInfo, System.IO.FileSystem\",\"Id\":\"safe\",\"Tables\":[]}"));
            Assert.IsType<PdmInfo>(new PdmJsonReader().Read(typed));
        }

        /// <summary>
        /// 验证日期形态的普通文本在 JSON 往返后保持原文。
        /// </summary>
        [Fact]
        public void PreservesDateShapedTextAndDateProperties()
        {
            const string dateText = "2026-10-04T00:00:00Z";
            var created = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc);
            var source = new PdmInfo { Id = "date-model", Comment = dateText, CreationDate = created, ModificationDate = created };
            var table = new TableInfo { Id = "date-table", Code = "DateTable" };
            table.Columns.Add(new ColumnInfo { Id = "date-column", Code = "DateColumn", DataType = "nvarchar", Comment = dateText, DefaultValue = dateText });
            source.Tables.Add(table);
            using var writer = new StringWriter();
            new PdmExporter().Write(source, PdmExportFormat.Json, writer);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString()));
            var restored = new PdmJsonReader().Read(stream);

            Assert.Equal(dateText, restored.Comment);
            Assert.Equal(dateText, restored.Tables[0].Columns[0].Comment);
            Assert.Equal(dateText, restored.Tables[0].Columns[0].DefaultValue);
            Assert.Equal(created, restored.CreationDate);
            Assert.Equal(created, restored.ModificationDate);
        }

        /// <summary>
        /// 验证损坏或无模型结构的 JSON 明确报错。
        /// </summary>
        [Theory]
        [InlineData("{")]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("{\"Id\":\"m\"}")]
        public void RejectsInvalidModelJson(string json)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            Assert.ThrowsAny<Exception>(() => new PdmJsonReader().Read(stream));
            Assert.True(stream.CanRead);
        }

        /// <summary>
        /// 验证 CLI 的 JSON 输入、生成预检和覆盖保护。
        /// </summary>
        [Fact]
        public void CliReusesJsonAndPreflightsGeneration()
        {
            var root = FindRepositoryRoot();
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
            var tool = Path.Combine(root, "samples", "Bing.Pdm.Tool", "bin", configuration, "net8.0", "Bing.Pdm.Tool.dll");
            var work = Path.Combine(Path.GetTempPath(), "Bing.Pdm.JsonTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                var fromPdm = Path.Combine(work, "from-pdm");
                var fromJson = Path.Combine(work, "from-json");
                Assert.Equal(0, RunDotnet(work, tool, "export", CompleteFixture, fromPdm, "json,md,html,svg").ExitCode);
                var json = Path.Combine(fromPdm, "complete.json");
                Assert.Equal(0, RunDotnet(work, tool, "export", json, fromJson, "md,html,svg,xlsx,docx").ExitCode);
                foreach (var suffix in new[] { ".md", ".html", "-Overview.svg" })
                    Assert.Equal(File.ReadAllText(Path.Combine(fromPdm, "complete" + suffix)), File.ReadAllText(Path.Combine(fromJson, "complete" + suffix)));
                Assert.True(File.Exists(Path.Combine(fromJson, "complete.xlsx")));
                Assert.True(File.Exists(Path.Combine(fromJson, "complete.docx")));

                var entitiesPdm = Path.Combine(work, "entities-pdm");
                var entitiesJson = Path.Combine(work, "entities-json");
                Assert.Equal(0, RunDotnet(work, tool, "generate", CompleteFixture, entitiesPdm, "Fixture.Entities").ExitCode);
                Assert.Equal(0, RunDotnet(work, tool, "generate", json, entitiesJson, "Fixture.Entities").ExitCode);
                foreach (var file in Directory.GetFiles(entitiesPdm, "*.cs"))
                    Assert.Equal(File.ReadAllText(file), File.ReadAllText(Path.Combine(entitiesJson, Path.GetFileName(file))));
                var project = Path.Combine(entitiesJson, "Generated.csproj");
                File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
                Assert.Equal(0, RunDotnet(work, "build", project, "--nologo", "--verbosity", "quiet").ExitCode);

                var before = File.ReadAllText(json);
                var collision = RunDotnet(work, tool, "export", json, fromPdm, "json,md");
                Assert.Equal(1, collision.ExitCode);
                Assert.Contains("overwrite the input", collision.StandardError);
                Assert.Equal(before, File.ReadAllText(json));
                foreach (var extension in new[] { "md", "xlsx", "docx" })
                {
                    var input = Path.Combine(work, "protected." + extension);
                    File.Copy(json, input);
                    var result = RunDotnet(work, tool, "export", input, work,
                        extension == "md" ? "html,md" : extension, "en", "--input-format", "json");
                    Assert.Equal(1, result.ExitCode);
                    Assert.Contains("overwrite the input", result.StandardError);
                    Assert.Equal(before, File.ReadAllText(input));
                    if (extension == "md") Assert.False(File.Exists(Path.Combine(work, "protected.html")));
                }
                var entityInput = Path.Combine(work, "Order.cs");
                File.Copy(json, entityInput);
                var entityCollision = RunDotnet(work, tool, "generate", entityInput, work,
                    "Fixture.Entities", "--input-format", "json");
                Assert.Equal(1, entityCollision.ExitCode);
                Assert.Contains("overwrite the input", entityCollision.StandardError);
                Assert.Equal(before, File.ReadAllText(entityInput));
                Assert.False(File.Exists(Path.Combine(work, "Customer.cs")));
                var extensionless = Path.Combine(work, "model-source");
                File.Copy(json, extensionless);
                Assert.Equal(0, RunDotnet(work, tool, "generate", extensionless, Path.Combine(work, "extensionless"), "Fixture.Entities", "--input-format", "json").ExitCode);
                Assert.Equal(1, RunDotnet(work, tool, "export", extensionless, Path.Combine(work, "unknown"), "md").ExitCode);
                Assert.False(Directory.Exists(Path.Combine(work, "unknown")));

                var badInput = Path.Combine(work, "bad-types.pdm");
                File.WriteAllText(badInput, File.ReadAllText(CompleteFixture).Replace("<a:DataType>nvarchar</a:DataType>", "<a:DataType>geography</a:DataType>").Replace("<a:DataType>int</a:DataType>", "<a:DataType>interger</a:DataType>"));
                var strictOutput = Path.Combine(work, "strict");
                var strict = RunDotnet(work, tool, "generate", badInput, strictOutput, "Fixture.Entities");
                Assert.Equal(1, strict.ExitCode);
                Assert.True(strict.StandardError.Split(new[] { "TYPE_ERROR" }, StringSplitOptions.None).Length > 2);
                Assert.False(Directory.Exists(strictOutput));
                var fallbackOutput = Path.Combine(work, "fallback");
                var fallback = RunDotnet(work, tool, "generate", badInput, fallbackOutput, "Fixture.Entities", "--fallback-type", "object", "--map", "*:geography=string");
                Assert.Equal(0, fallback.ExitCode);
                Assert.Contains("TYPE_FALLBACK", fallback.StandardError);
                Assert.Contains("public string DisplayNote", File.ReadAllText(Path.Combine(fallbackOutput, "Order.cs")));
                Assert.Equal(0, RunDotnet(work, tool, "export", json, Path.Combine(work, "explicit-format"), "md", "en", "--input-format", "json").ExitCode);
            }
            finally
            {
                if (Directory.Exists(work)) Directory.Delete(work, true);
            }
        }

        /// <summary>
        /// 查找解决方案所在目录。
        /// </summary>
        /// <returns>仓库根目录。</returns>
        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Bing.Pdm.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException("Bing.Pdm.sln was not found.");
        }

        /// <summary>
        /// 运行 CLI 并读取进程结果。
        /// </summary>
        /// <param name="workingDirectory">命令工作目录。</param>
        /// <param name="arguments">传给 dotnet 的参数。</param>
        /// <returns>退出码和标准错误。</returns>
        private static (int ExitCode, string StandardError) RunDotnet(string workingDirectory, params string[] arguments)
        {
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using (var process = Process.Start(start))
            {
                Assert.NotNull(process);
                process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return (process.ExitCode, error);
            }
        }
    }
}
