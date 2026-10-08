using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Bing.Pdm;
using Bing.Pdm.Models;
using Bing.Pdm.Models.Others;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证模型结构校验与不完整差异报告。
    /// </summary>
    public sealed class PdmModelValidationTests
    {
        /// <summary>
        /// 验证有效模型不产生问题且不会重建或修改读取索引。
        /// </summary>
        [Fact]
        public void ValidModelDoesNotMutateLookupOrDiagnostics()
        {
            var model = ValidModel();
            model.RebuildLookup();
            var lookup = model.Lookup;
            var diagnostics = model.Diagnostics.ToArray();
            var result = new PdmModelValidator().Validate(model);

            Assert.True(result.IsValid);
            Assert.Empty(result.Issues);
            Assert.Same(lookup, model.Lookup);
            Assert.Equal(diagnostics, model.Diagnostics);
        }

        /// <summary>
        /// 验证删除列后的校验不依赖过期索引。
        /// </summary>
        [Fact]
        public void RemovedColumnIsDetectedWithoutRebuildingLookup()
        {
            var model = ValidModel();
            model.RebuildLookup();
            var lookup = model.Lookup;
            model.Tables[0].Columns.Clear();
            Assert.True(lookup.TryGetColumn("order-id", out _));
            var result = new PdmModelValidator().Validate(model);
            Assert.Contains(result.Issues, x => x.Code == "UNRESOLVED_MODEL_REF" && x.TargetId == "order-id");
            Assert.Same(lookup, model.Lookup);
            Assert.Empty(model.Diagnostics);
        }

        /// <summary>
        /// 验证重复局部标识、重复 GUID 和空局部标识均可诊断。
        /// </summary>
        [Fact]
        public void ReportsDuplicateAndMissingObjectIdentity()
        {
            var model = ValidModel();
            model.Tables.Add(new TableInfo { Id = "orders", ObjectId = "table-guid" });
            model.Tables.Add(new TableInfo { Id = null, ObjectId = "table-guid" });

            var issues = new PdmModelValidator().Validate(model).Issues;
            Assert.Contains(issues, x => x.Code == "DUPLICATE_ID");
            Assert.Contains(issues, x => x.Code == "DUPLICATE_OBJECT_ID");
            Assert.Contains(issues, x => x.Code == "MISSING_OBJECT_ID");
        }

        /// <summary>
        /// 验证键、索引和外键列必须属于相应表。
        /// </summary>
        [Fact]
        public void ReportsCrossTableColumnOwnershipErrors()
        {
            var model = ValidModel();
            var other = new TableInfo { Id = "other", ObjectId = "other-guid" };
            other.Columns.Add(new ColumnInfo { Id = "other-id", ObjectId = "other-column-guid" });
            model.Tables.Add(other);
            model.Tables[0].Keys.Single().ColumnIds.Add("other-id");
            model.Tables[0].Indexes.Single().ColumnIds.Add("other-id");
            model.References.Single().Joins.Single().ChildColumnId = "other-id";

            var issues = new PdmModelValidator().Validate(model).Issues;
            Assert.Contains(issues, x => x.Code == "INVALID_REFERENCE_OWNER");
            Assert.True(issues.Count(x => x.Code == "INVALID_REFERENCE_OWNER") >= 3);
        }

        /// <summary>
        /// 验证缺失表端点和无上下文外部地址分别产生诊断。
        /// </summary>
        [Fact]
        public void ReportsUnresolvedAndUnverifiedExternalReferences()
        {
            var model = ValidModel();
            model.References.Single().ParentTableId = "missing";
            var unresolved = new PdmModelValidator().Validate(model).Issues;
            Assert.Contains(unresolved, x => x.Code == "UNRESOLVED_MODEL_REF");

            var external = ValidModel();
            external.References.Single().ParentTableAddress = new PdmObjectAddress
            { ModelKey = "warehouse", PdmId = "orders" };
            var externalIssues = new PdmModelValidator().Validate(external).Issues;
            Assert.Contains(externalIssues, x => x.Code == "EXTERNAL_TARGET_UNVERIFIED");
        }

        /// <summary>
        /// 验证图元问题由独立校验报告且不影响结构比较。
        /// </summary>
        [Fact]
        public void DiagramValidationDoesNotBecomeStructureChange()
        {
            var before = ValidModel();
            var after = ValidModel();
            after.PhysicalDiagrams.Single().Symbols.Single().ObjectId = "missing-symbol-object";
            var diff = new PdmModelComparer().Compare(before, after);

            Assert.Empty(diff.Changes);
            Assert.False(diff.Incomplete);
            Assert.Empty(diff.ValidationIssues);
            var diagramIssues = new PdmModelValidator().Validate(after).Issues;
            Assert.Contains(diagramIssues, x => x.Scope == "Diagram");
        }

        /// <summary>
        /// 验证工作区地址能解析同 ID 的不同模型。
        /// </summary>
        [Fact]
        public void WorkspaceContextResolvesSameLocalIdAcrossModels()
        {
            var source = ValidModel();
            var target = ValidModel();
            target.ObjectId = "target-model-guid";
            source.References.Single().ParentTableAddress = new PdmObjectAddress
            { ModelKey = "target", PdmId = "orders" };
            var workspace = new PdmWorkspace();
            workspace.Add("source", source);
            workspace.Add("target", target);

            var result = new PdmModelValidator().Validate(source, workspace, "source");
            Assert.DoesNotContain(result.Issues, x => x.Code == "EXTERNAL_TARGET_UNVERIFIED");
            Assert.DoesNotContain(result.Issues, x => x.Code == "UNRESOLVED_MODEL_REF");
        }

        /// <summary>
        /// 验证集合环不会导致比较递归溢出，并会标记结构不完整。
        /// </summary>
        [Fact]
        public void CollectionCycleMakesDiffIncompleteWithoutOverflow()
        {
            var before = ValidModel();
            var after = ValidModel();
            var packageBefore = new PackageInfo { Id = "cycle" };
            var packageAfter = new PackageInfo { Id = "cycle" };
            packageBefore.Packages.Add(packageBefore);
            packageAfter.Packages.Add(packageAfter);
            before.Packages.Add(packageBefore);
            after.Packages.Add(packageAfter);

            var diff = new PdmModelComparer().Compare(before, after);
            Assert.True(diff.Incomplete);
        }

        /// <summary>
        /// 验证差异报告在 Markdown 和 HTML 中转义问题文本。
        /// </summary>
        [Fact]
        public void DiffReportsEscapeValidationIssueText()
        {
            var before = ValidModel();
            var after = ValidModel();
            before.References.Single().ParentTableId = "<missing&";
            after.References.Single().ParentTableId = "<missing&";
            var diff = new PdmModelComparer().Compare(before, after);
            using (var markdown = new StringWriter())
            using (var html = new StringWriter())
            {
                new PdmDiffExporter().Write(diff, PdmExportFormat.Markdown, markdown);
                new PdmDiffExporter().Write(diff, PdmExportFormat.Html, html);
                Assert.DoesNotContain("<missing&", markdown.ToString());
                Assert.DoesNotContain("<missing&", html.ToString());
            }
        }

        /// <summary>
        /// 验证 CLI 默认保留不完整报告并由显式开关决定退出码。
        /// </summary>
        [Fact]
        public void CliWritesIncompleteReportsAndSupportsFailOnIncomplete()
        {
            var root = FindRepositoryRoot();
            var tool = Path.Combine(root, "samples", "Bing.Pdm.Tool", "bin", "Debug", "net8.0",
                "Bing.Pdm.Tool.dll");
            var fixture = Path.Combine(root, "tests", "Bing.Pdm.Tests", "Fixtures", "complete.pdm");
            var directory = Path.Combine(Path.GetTempPath(), "pdm-validation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var normal = Run(tool, "diff", fixture, fixture, Path.Combine(directory, "normal"), "json");
                Assert.Equal(0, normal.ExitCode);
                Assert.True(File.Exists(normal.Report));
                Assert.Contains("ValidationIssues", File.ReadAllText(normal.Report));

                var strict = Run(tool, "diff", fixture, fixture, Path.Combine(directory, "strict"), "json",
                    "--fail-on-incomplete");
                Assert.Equal(1, strict.ExitCode);
                Assert.True(File.Exists(strict.Report));
            }
            finally { Directory.Delete(directory, true); }
        }

        /// <summary>
        /// 构造具有表、列、键、索引、引用和图元的最小有效模型。
        /// </summary>
        private static PdmInfo ValidModel()
        {
            var model = new PdmInfo { Id = "model", ObjectId = "model-guid" };
            var table = new TableInfo { Id = "orders", ObjectId = "table-guid", Name = "Orders" };
            table.Columns.Add(new ColumnInfo { Id = "order-id", ObjectId = "column-guid", Name = "Id" });
            table.Keys.Add(new Bing.Pdm.Models.Keys.KeyInfo
            {
                Id = "pk-orders",
                ObjectId = "key-guid",
                ColumnIds = { "order-id" }
            });
            table.PrimaryKeyId = "pk-orders";
            table.Indexes.Add(new IndexInfo
            {
                Id = "ix-orders",
                ObjectId = "index-guid",
                ColumnIds = { "order-id" }
            });
            model.Tables.Add(table);
            model.References.Add(new ReferenceInfo
            {
                Id = "ref-orders",
                ObjectId = "ref-guid",
                ParentTableId = "orders",
                ChildTableId = "orders",
                ParentKeyId = "pk-orders",
                Joins =
                { new ReferenceJoinInfo { Id = "join-orders", ParentColumnId = "order-id", ChildColumnId = "order-id" } }
            });
            var diagram = new Bing.Pdm.Models.PhysicalDiagrams.PhysicalDiagramInfo { Id = "diagram" };
            diagram.Symbols.Add(new Bing.Pdm.Models.PhysicalDiagrams.DiagramSymbolInfo
            { Id = "symbol", Kind = "TableSymbol", ObjectId = "orders" });
            model.PhysicalDiagrams.Add(diagram);
            return model;
        }

        /// <summary>
        /// 定位仓库根目录。
        /// </summary>
        private static string FindRepositoryRoot()
        {
            var path = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(path, "Bing.Pdm.slnx")))
                path = Directory.GetParent(path).FullName;
            return path;
        }

        /// <summary>
        /// 运行 CLI 差异命令并返回报告路径和退出码。
        /// </summary>
        private static (int ExitCode, string Report) Run(string tool, params string[] arguments)
        {
            var output = arguments[3];
            var report = Path.Combine(output,
                Path.GetFileNameWithoutExtension(arguments[1]) + "-to-" +
                Path.GetFileNameWithoutExtension(arguments[2]) + ".json");
            var process = Process.Start(new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = "\"" + tool + "\" " + string.Join(" ", arguments.Select(Quote))
            });
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, report);
        }

        /// <summary>
        /// 为 CLI 参数添加引号。
        /// </summary>
        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
