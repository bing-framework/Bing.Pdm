using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Bing.Pdm.Models;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证跨工作区模型身份与关系端点配对。
    /// </summary>
    public sealed class PdmWorkspaceIdentityTests
    {
        /// <summary>
        /// 验证工作区清单键变化不影响相同模型 GUID 的关系匹配。
        /// </summary>
        [Fact]
        public void DifferentWorkspaceKeysPreserveForeignKeyAndJoinIdentity()
        {
            var before = Scenario("source", "dependency", "dependency-guid");
            var after = Scenario("source", "dependency-renamed", "dependency-guid");
            var result = new PdmModelComparer().Compare(before.Source, after.Source,
                new PdmCompareOptions
                {
                    BeforeWorkspace = before.Workspace,
                    AfterWorkspace = after.Workspace,
                    BeforeModelKey = "source",
                    AfterModelKey = "source"
                });

            Assert.Empty(result.Changes);
            Assert.False(result.Incomplete);
        }

        /// <summary>
        /// 验证等价 GUID 表示在重命名时保持对象配对。
        /// </summary>
        [Fact]
        public void EquivalentObjectGuidsPreserveRenameIdentity()
        {
            const string guid = "9f4a2e1c-7b8d-4a10-9c22-1234567890ab";
            var before = new PdmInfo { Id = "before" };
            var after = new PdmInfo { Id = "after" };
            before.Tables.Add(new TableInfo { Id = "old", ObjectId = guid, Code = "Old" });
            after.Tables.Add(new TableInfo { Id = "new", ObjectId = "{" + guid.ToUpperInvariant() + "}", Code = "New" });
            var diff = new PdmModelComparer().Compare(before, after);
            Assert.DoesNotContain(diff.Changes, x => x.ChangeType == "Added" || x.ChangeType == "Removed");
            Assert.Contains(diff.Changes, x => x.Property == "Code");
            Assert.False(diff.Incomplete);
        }

        /// <summary>
        /// 验证缺少地址对象 GUID 时从显式工作区集合定位端点。
        /// </summary>
        [Fact]
        public void MissingAddressObjectIdUsesWorkspaceCollectionIdentity()
        {
            var before = Scenario("source", "dependency", "dependency-guid", addressObjectId: null);
            var after = Scenario("source", "dependency-renamed", "dependency-guid", addressObjectId: null);
            var result = new PdmModelComparer().Compare(before.Source, after.Source,
                new PdmCompareOptions
                {
                    BeforeWorkspace = before.Workspace,
                    AfterWorkspace = after.Workspace,
                    BeforeModelKey = "source",
                    AfterModelKey = "source"
                });

            Assert.Empty(result.Changes);
            Assert.False(result.Incomplete);
        }

        /// <summary>
        /// 验证 GUID 大小写、花括号和 D 格式等价且工作区拒绝重复身份。
        /// </summary>
        [Fact]
        public void EquivalentModelGuidsAreCanonicalAndDuplicateKeysAreRejected()
        {
            const string guid = "9F4A2E1C-7B8D-4A10-9C22-1234567890AB";
            var first = new PdmInfo { Id = "first", ObjectId = guid };
            var equivalent = new PdmInfo { Id = "second", ObjectId = "{" + guid.ToLowerInvariant() + "}" };
            var workspace = new PdmWorkspace();
            workspace.Add("first", first);
            Assert.Throws<ArgumentException>(() => workspace.Add("second", equivalent));
        }

        /// <summary>
        /// 验证目标模型标识不同仍可通过会话目标解析同一依赖 GUID。
        /// </summary>
        [Fact]
        public void DifferentTargetModelIdsCanResolveEquivalentDependencyGuid()
        {
            const string guid = "9f4a2e1c-7b8d-4a10-9c22-1234567890ab";
            var scenario = Scenario("source", "dependency", guid);
            scenario.Source.RebuildLookup();
            scenario.Dependency.RebuildLookup();
            var shortcut = new PdmShortcutInfo { Id = "shortcut", TargetKind = "Table", TargetId = "PARENT-GUID" };
            scenario.Source.Shortcuts.Add(shortcut);
            scenario.Source.TargetModels.Add(new Bing.Pdm.Models.Others.TargetModelInfo
            {
                Id = "target-session",
                TargetModelId = "{" + guid.ToUpperInvariant() + "}",
                SessionShortcutRefs = { shortcut.Id }
            });
            scenario.Source.RebuildLookup();
            new PdmWorkspaceResolver().Resolve(scenario.Workspace);
            Assert.Equal("dependency", shortcut.ResolvedTargetAddress.ModelKey);
            Assert.Equal("parent", shortcut.ResolvedTargetAddress.PdmId);
            Assert.DoesNotContain(scenario.Workspace.GetDiagnostics("source"), x => x.Code.Contains("WORKSPACE_MODEL"));
        }

        /// <summary>
        /// 验证真正更换依赖模型 GUID会报告外键端点和 Join 变化。
        /// </summary>
        [Fact]
        public void ChangedDependencyModelGuidReportsEndpointAndJoinChanges()
        {
            var before = Scenario("source", "dependency", "dependency-guid");
            var after = Scenario("source", "dependency-renamed", "different-dependency-guid");
            var result = new PdmModelComparer().Compare(before.Source, after.Source,
                new PdmCompareOptions
                {
                    BeforeWorkspace = before.Workspace,
                    AfterWorkspace = after.Workspace,
                    BeforeModelKey = "source",
                    AfterModelKey = "source"
                });

            Assert.Contains(result.Changes, x => x.Property == "ParentTable");
            Assert.Contains(result.Changes, x => x.Property == "Joins");
        }

        /// <summary>
        /// 验证 CLI 两侧清单使用不同别名时仍能比较同一工作区模型。
        /// </summary>
        [Fact]
        public void CliDiffAcceptsDifferentWorkspaceAliases()
        {
            var root = FindRepositoryRoot();
            var tool = Path.Combine(root, "samples", "Bing.Pdm.Tool", "bin", "Debug", "net8.0",
                "Bing.Pdm.Tool.dll");
            var sales = Path.Combine(root, "tests", "Bing.Pdm.Tests", "Fixtures", "workspace-sales.pdm");
            var warehouse = Path.Combine(root, "tests", "Bing.Pdm.Tests", "Fixtures", "workspace-warehouse.pdm");
            var directory = Path.Combine(Path.GetTempPath(), "pdm-workspace-cli-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var before = WriteManifest(directory, "before.json", "sales-before", "warehouse-before", sales, warehouse);
                var after = WriteManifest(directory, "after.json", "sales-after", "warehouse-after", sales, warehouse);
                var output = Path.Combine(directory, "reports");
                var arguments = new[] { tool, "diff", sales, sales, output, "json", "--fail-on-change",
                    "--before-workspace", before, "--after-workspace", after };
                Assert.Equal(0, RunCli(arguments).ExitCode);
                var report = Path.Combine(output, "workspace-sales-to-workspace-sales.json");
                Assert.True(File.Exists(report));
                Assert.Contains("\"Incomplete\": false", File.ReadAllText(report));

                var strict = RunCli(arguments.Append("--fail-on-incomplete").ToArray());
                Assert.Equal(0, strict.ExitCode);
                Assert.True(File.Exists(report));
            }
            finally { DeleteDirectory(directory); }
        }

        /// <summary>
        /// 验证重复模型 GUID 清单被作为输入错误拒绝而非参数错误。
        /// </summary>
        [Fact]
        public void CliRejectsDuplicateWorkspaceGuidsAsInputError()
        {
            var root = FindRepositoryRoot();
            var tool = Path.Combine(root, "samples", "Bing.Pdm.Tool", "bin", "Debug", "net8.0",
                "Bing.Pdm.Tool.dll");
            var sales = Path.Combine(root, "tests", "Bing.Pdm.Tests", "Fixtures", "workspace-sales.pdm");
            var directory = Path.Combine(Path.GetTempPath(), "pdm-workspace-duplicate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var manifest = WriteManifest(directory, "duplicate.json", "one", "two", sales, sales);
                var result = RunCli(tool, "diff", sales, sales, Path.Combine(directory, "reports"), "json",
                    "--before-workspace", manifest, "--after-workspace", manifest);
                Assert.Equal(1, result.ExitCode);
                Assert.NotEqual(2, result.ExitCode);
            }
            finally { DeleteDirectory(directory); }
        }

        /// <summary>
        /// 构造包含本地子表和跨模型父表的最小工作区。
        /// </summary>
        private static ScenarioData Scenario(string sourceKey, string dependencyKey,
            string dependencyGuid, string addressObjectId = "parent-guid")
        {
            var source = new PdmInfo { Id = "source-pdm", ObjectId = "source-guid" };
            var dependency = new PdmInfo { Id = "dependency-model", ObjectId = dependencyGuid };
            var parent = new TableInfo { Id = "parent", ObjectId = "parent-guid" };
            var parentColumn = new ColumnInfo { Id = "parent-id", ObjectId = "parent-column-guid" };
            parent.Columns.Add(parentColumn);
            parent.Keys.Add(new Bing.Pdm.Models.Keys.KeyInfo
            {
                Id = "parent-pk",
                ObjectId = "parent-key-guid",
                ColumnIds = { parentColumn.Id }
            });
            parent.PrimaryKeyId = "parent-pk";
            dependency.Tables.Add(parent);
            var child = new TableInfo { Id = "child", ObjectId = "child-guid" };
            var childColumn = new ColumnInfo { Id = "parent-id", ObjectId = "child-column-guid" };
            child.Columns.Add(childColumn);
            source.Tables.Add(child);
            source.References.Add(new ReferenceInfo
            {
                Id = "reference",
                ObjectId = "reference-guid",
                ParentTableId = "parent",
                ChildTableId = "child",
                ParentKeyId = "parent-pk",
                ParentTableAddress = new PdmObjectAddress
                {
                    ModelKey = dependencyKey,
                    PdmId = parent.Id,
                    ObjectId = addressObjectId
                },
                ChildTableAddress = new PdmObjectAddress
                {
                    ModelKey = sourceKey,
                    PdmId = child.Id,
                    ObjectId = child.ObjectId
                },
                ParentKeyAddress = new PdmObjectAddress
                {
                    ModelKey = dependencyKey,
                    PdmId = parent.PrimaryKeyId,
                    ObjectId = "parent-key-guid"
                },
                Joins = { new ReferenceJoinInfo { Id = "join", ParentColumnId = "parent-id",
                    ChildColumnId = "parent-id", ParentColumnAddress = new PdmObjectAddress
                    { ModelKey = dependencyKey, PdmId = parentColumn.Id, ObjectId = "parent-column-guid" },
                    ChildColumnAddress = new PdmObjectAddress { ModelKey = sourceKey, PdmId = childColumn.Id,
                        ObjectId = childColumn.ObjectId } } }
            });
            var workspace = new PdmWorkspace();
            workspace.Add(sourceKey, source);
            workspace.Add(dependencyKey, dependency);
            return new ScenarioData(source, dependency, workspace);
        }

        /// <summary>
        /// 写入使用绝对 PDM 路径的工作区清单。
        /// </summary>
        private static string WriteManifest(string directory, string name, string sourceKey,
            string dependencyKey, string sourcePath, string dependencyPath)
        {
            var json = "{\"WorkspaceVersion\":1,\"Models\":[" +
                "{\"Key\":\"" + sourceKey + "\",\"Path\":\"" + EscapeJson(sourcePath) + "\"}," +
                "{\"Key\":\"" + dependencyKey + "\",\"Path\":\"" + EscapeJson(dependencyPath) + "\"}]}";
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, json, new UTF8Encoding(false));
            return path;
        }

        /// <summary>
        /// 转义 JSON 字符串内容。
        /// </summary>
        private static string EscapeJson(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        /// <summary>
        /// 定位仓库根目录。
        /// </summary>
        private static string FindRepositoryRoot()
        {
            var path = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(path, "Bing.Pdm.sln")))
                path = Directory.GetParent(path).FullName;
            return path;
        }

        /// <summary>
        /// 运行 CLI 并返回退出码与输出。
        /// </summary>
        private static (int ExitCode, string StdOut, string StdErr) RunCli(params string[] arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            process.Start();
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, stdout, stderr);
        }

        /// <summary>
        /// 删除测试临时目录。
        /// </summary>
        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }

        /// <summary>
        /// 保存一个来源模型、依赖模型及其工作区。
        /// </summary>
        private sealed class ScenarioData
        {
            /// <summary>
            /// 初始化一个 <see cref="ScenarioData"/> 类型的实例。
            /// </summary>
            public ScenarioData(PdmInfo source, PdmInfo dependency, PdmWorkspace workspace)
            {
                Source = source;
                Dependency = dependency;
                Workspace = workspace;
            }

            /// <summary>
            /// 获取来源模型。
            /// </summary>
            public PdmInfo Source { get; }
            /// <summary>
            /// 获取依赖模型。
            /// </summary>
            public PdmInfo Dependency { get; }
            /// <summary>
            /// 获取模型工作区。
            /// </summary>
            public PdmWorkspace Workspace { get; }
        }
    }
}
