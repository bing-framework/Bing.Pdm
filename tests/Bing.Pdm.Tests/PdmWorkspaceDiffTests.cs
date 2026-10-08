using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Bing.Pdm;
using Bing.Pdm.Models;
using Bing.Pdm.Models.Others;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Reader;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证显式工作区和结构比较。
    /// </summary>
    public sealed class PdmWorkspaceDiffTests
    {
        /// <summary>
        /// 获取脱敏模型。
        /// </summary>
        private static PdmInfo Model() => new PdmReader().ReadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "real-compat.pdm"));

        /// <summary>
        /// 验证两个模型中的同名本地 ID 不会误匹配。
        /// </summary>
        [Fact]
        public void WorkspaceResolvesShortcutByGuidAndKeepsLocalIdsScoped()
        {
            var sales = Model();
            var warehouse = Model();
            sales.ObjectId = "sales-model";
            warehouse.ObjectId = "warehouse-model";
            warehouse.AllTables.First(x => x.Id == "target-table").ObjectId = "warehouse-target-guid";
            sales.AllShortcuts.Single().TargetId = "warehouse-target-guid";
            sales.RebuildLookup();
            warehouse.RebuildLookup();
            var workspace = new PdmWorkspace();
            workspace.Add("sales", sales);
            workspace.Add("warehouse", warehouse);
            new PdmWorkspaceResolver().Resolve(workspace);
            var reference = sales.AllReferences.Single();
            Assert.Equal("warehouse", sales.AllShortcuts.Single().ResolvedTargetAddress.ModelKey);
            Assert.Equal("warehouse", reference.ParentTableAddress.ModelKey);
            Assert.Equal("sales", reference.ChildTableAddress.ModelKey);
            Assert.Equal("warehouse", reference.ParentKeyAddress.ModelKey);
            Assert.Empty(workspace.GetDiagnostics("sales").Where(x => x.Code == "UNRESOLVED_SHORTCUT_TARGET"));
            using (var output = new StringWriter())
            {
                new PdmExporter().Write(workspace, "sales", PdmExportFormat.Html, output);
                Assert.Contains("warehouse:TargetTable", output.ToString());
                Assert.DoesNotContain("href=\"#table-target-table\"", output.ToString());
            }
        }

        /// <summary>
        /// 验证双模型清单中关系、列和图元的限定地址。
        /// </summary>
        [Fact]
        public void FixtureManifestResolvesCrossModelReference()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "workspace.json");
            var workspace = new PdmWorkspaceReader().ReadFromFile(path);
            var sales = workspace.Models["sales"];
            Assert.Single(sales.AllTables);
            Assert.Equal("warehouse", sales.AllShortcuts.Single().ResolvedTargetAddress.ModelKey);
            var reference = sales.AllReferences.Single();
            Assert.Equal("warehouse", reference.ParentTableAddress.ModelKey);
            Assert.Equal("sales", reference.ChildTableAddress.ModelKey);
            Assert.Equal("warehouse", reference.ParentKeyAddress.ModelKey);
            Assert.Equal("warehouse", reference.Joins.Single().ParentColumnAddress.ModelKey);
            Assert.Equal("sales", reference.Joins.Single().ChildColumnAddress.ModelKey);
            Assert.Equal("warehouse", sales.AllPhysicalDiagrams.Single().AllSymbols.Single().ObjectAddress.ModelKey);
            Assert.Empty(workspace.GetDiagnostics("sales"));
            using (var output = new StringWriter())
            {
                new PdmExporter().Write(workspace, "sales", PdmExportFormat.Json, output);
                Assert.Empty((JArray)JObject.Parse(output.ToString())["Diagnostics"]);
            }
            using (var output = new StringWriter())
            {
                new PdmExporter().Write(workspace, "sales", PdmExportFormat.Html, output);
                Assert.Contains("warehouse:Item", output.ToString());
                Assert.DoesNotContain("href=\"#table-o123\"", output.ToString());
            }
            using (var output = new StringWriter())
            {
                new PdmExporter().WriteDiagram(workspace, "sales",
                    sales.AllPhysicalDiagrams.Single(), output);
                Assert.Contains("warehouse:Item", output.ToString());
            }
            using (var output = new MemoryStream())
            {
                new PdmOfficeExporter().WriteExcel(workspace, "sales", output);
                output.Position = 0;
                using (var archive = new ZipArchive(output, ZipArchiveMode.Read, true))
                    Assert.Contains(archive.Entries, entry => ReadEntry(entry).Contains("warehouse:Item"));
            }
            using (var output = new MemoryStream())
            {
                new PdmOfficeExporter().WriteWord(workspace, "sales", output);
                output.Position = 0;
                using (var archive = new ZipArchive(output, ZipArchiveMode.Read, true))
                    Assert.Contains("warehouse:Item", ReadEntry(archive.GetEntry("word/document.xml")));
            }
        }

        /// <summary>
        /// 验证 Shortcut 环停止解析且诊断不重复累积。
        /// </summary>
        [Fact]
        public void WorkspaceDetectsShortcutCycleIdempotently()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "workspace.json");
            var workspace = new PdmWorkspaceReader().ReadFromFile(path);
            var sales = workspace.Models["sales"];
            var first = sales.AllShortcuts.Single();
            sales.TargetModels.Single().SessionShortcutRefs.Clear();
            var second = new PdmShortcutInfo
            {
                Id = "sc2",
                ObjectId = "second-shortcut-guid",
                TargetId = first.ObjectId,
                TargetKind = "Table"
            };
            first.TargetId = second.ObjectId;
            sales.Shortcuts.Add(second);
            sales.RebuildLookup();
            var resolver = new PdmWorkspaceResolver();
            resolver.Resolve(workspace);
            Assert.Contains(workspace.GetDiagnostics("sales"), x => x.Code == "CYCLIC_SHORTCUT");
            var count = workspace.GetDiagnostics("sales").Count();
            resolver.Resolve(workspace);
            Assert.Equal(count, workspace.GetDiagnostics("sales").Count());
        }

        /// <summary>
        /// 验证复制来源只解析到会话指定的真实模型。
        /// </summary>
        [Fact]
        public void ReplicationOriginUsesSessionModelAndExcludesEmbeddedObjects()
        {
            var source = new PdmInfo { Id = "source", ObjectId = "source-guid" };
            var target = new PdmInfo { Id = "target", ObjectId = "target-guid" };
            target.Tables.Add(new TableInfo { Id = "table", ObjectId = "table-guid", Code = "Table" });
            var session = new TargetModelInfo { Id = "session", TargetModelId = target.ObjectId };
            session.SessionReplicationRefs.Add("replication");
            session.EmbeddedObjects.Add(new PdmEmbeddedObjectInfo
            {
                Id = "embedded",
                ObjectId = "table-guid",
                Kind = "Table"
            });
            source.TargetModels.Add(session);
            var replication = new PdmReplicationInfo { Id = "replication", OriginalId = "table-guid" };
            source.Replications.Add(replication);
            source.RebuildLookup();
            target.RebuildLookup();
            var workspace = new PdmWorkspace();
            workspace.Add("source", source);
            workspace.Add("target", target);
            var resolver = new PdmWorkspaceResolver();
            resolver.Resolve(workspace);
            Assert.Equal("target", replication.OriginalAddress.ModelKey);
            Assert.Equal("table", replication.OriginalAddress.PdmId);

            session.TargetModelId = "absent-model-guid";
            resolver.Resolve(workspace);
            Assert.Null(replication.OriginalAddress);
            Assert.Contains(workspace.GetDiagnostics("source"),
                x => x.Code == "UNRESOLVED_WORKSPACE_MODEL" && x.SourceId == replication.Id);
        }

        /// <summary>
        /// 验证 Shortcut 列所属表与会话目标的歧义诊断。
        /// </summary>
        [Fact]
        public void WorkspaceRejectsShortcutColumnFromAnotherTableAndAmbiguousSessions()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "workspace.json");
            var workspace = new PdmWorkspaceReader().ReadFromFile(path);
            var sales = workspace.Models["sales"];
            var reference = sales.AllReferences.Single();
            var localColumn = sales.AllTables.Single().Columns.First();
            var shortcut = new PdmShortcutInfo
            {
                Id = "wrong-column-shortcut",
                TargetId = localColumn.ObjectId,
                TargetKind = "Column"
            };
            sales.Shortcuts.Add(shortcut);
            reference.Joins.Single().ParentColumnId = shortcut.Id;
            sales.RebuildLookup();
            var resolver = new PdmWorkspaceResolver();
            resolver.Resolve(workspace);
            Assert.Null(reference.Joins.Single().ParentColumnAddress);
            Assert.Contains(workspace.GetDiagnostics("sales"), x =>
                x.Code == "INVALID_WORKSPACE_REFERENCE" && x.SourceId == reference.Joins.Single().Id);

            var original = sales.AllShortcuts.First(x => x.Id != shortcut.Id);
            var duplicateSession = new TargetModelInfo
            {
                Id = "second-session",
                TargetModelId = workspace.Models["warehouse"].ObjectId
            };
            duplicateSession.SessionShortcutRefs.Add(original.Id);
            sales.TargetModels.Add(duplicateSession);
            sales.RebuildLookup();
            resolver.Resolve(workspace);
            Assert.Null(original.ResolvedTargetAddress);
            Assert.Contains(workspace.GetDiagnostics("sales"), x =>
                x.Code == "AMBIGUOUS_WORKSPACE_MODEL" && x.SourceId == original.Id);
        }

        /// <summary>
        /// 验证复制副本可以保留 Shortcut 本身的地址。
        /// </summary>
        [Fact]
        public void ReplicationReplicaKeepsShortcutAddress()
        {
            var workspace = new PdmWorkspaceReader().ReadFromFile(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "workspace.json"));
            var sales = workspace.Models["sales"];
            var shortcut = sales.AllShortcuts.Single();
            var replication = new PdmReplicationInfo
            {
                Id = "copy",
                ReplicaObjectRef = shortcut.Id,
                ReplicaObjectKind = "Shortcut"
            };
            sales.Replications.Add(replication);
            sales.RebuildLookup();
            new PdmWorkspaceResolver().Resolve(workspace);
            Assert.Equal("sales", replication.ReplicaObjectAddress.ModelKey);
            Assert.Equal(shortcut.Id, replication.ReplicaObjectAddress.PdmId);
        }

        /// <summary>
        /// 验证视图、包和关系 Shortcut 的类型解析。
        /// </summary>
        [Fact]
        public void ReadsNonTableShortcutsFromTheirCollections()
        {
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "workspace-sales.pdm"));
            var extra = @"<c:Packages><o:Package Id=""pkg1""><a:ObjectID>pkg-guid</a:ObjectID>
                <a:Code>Pkg</a:Code></o:Package><o:Shortcut Id=""pkg-sc"">
                <a:TargetID>pkg-guid</a:TargetID><a:TargetClassID>PACKAGE</a:TargetClassID>
                </o:Shortcut></c:Packages>
                <c:Views><o:View Id=""view1""><a:ObjectID>view-guid</a:ObjectID>
                <a:Code>Recent</a:Code></o:View><o:Shortcut Id=""view-sc"">
                <a:TargetID>view-guid</a:TargetID><a:TargetClassID>VIEW</a:TargetClassID>
                </o:Shortcut></c:Views>";
            var xml = source.Replace("</o:Model>", extra + "</o:Model>");
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                var model = new PdmReader().Read(stream);
                Assert.Equal("pkg1", model.AllShortcuts.Single(x => x.Id == "pkg-sc").ResolvedTargetId);
                Assert.Equal("view1", model.AllShortcuts.Single(x => x.Id == "view-sc").ResolvedTargetId);
            }
        }

        /// <summary>
        /// 验证清单读取、重复模型身份和未知版本。
        /// </summary>
        [Fact]
        public void ManifestLoadsJsonAndRejectsDuplicateIdentity()
        {
            var directory = Path.Combine(Path.GetTempPath(), "pdm-workspace-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var model = Model();
                using (var writer = new StreamWriter(Path.Combine(directory, "a.json")))
                    new PdmExporter().Write(model, PdmExportFormat.Json, writer);
                File.WriteAllText(Path.Combine(directory, "workspace.json"),
                    "{\"WorkspaceVersion\":1,\"Models\":[{\"Key\":\"a\",\"Path\":\"a.json\"}]}");
                var workspace = new PdmWorkspaceReader().ReadFromFile(Path.Combine(directory, "workspace.json"));
                Assert.Equal("a", workspace.FindModelKeyByPath(Path.Combine(directory, "a.json")));
                Assert.Throws<ArgumentException>(() => workspace.Add("b", Model()));
                File.WriteAllText(Path.Combine(directory, "workspace.json"),
                    "{\"WorkspaceVersion\":2,\"Models\":[{\"Key\":\"a\",\"Path\":\"a.json\"}]}");
                Assert.Throws<InvalidDataException>(() => new PdmWorkspaceReader().ReadFromFile(Path.Combine(directory, "workspace.json")));
            }
            finally { Directory.Delete(directory, true); }
        }

        /// <summary>
        /// 验证结构比较的重命名和有序主键。
        /// </summary>
        [Fact]
        public void DiffRecognizesGuidRenameAndCompositeKeyOrder()
        {
            var before = Model();
            var after = Model();
            var table = after.AllTables.First(x => x.Id == "target-table");
            table.Name = "Renamed Target";
            table.Code = "RenamedTarget";
            var key = table.Keys.Single();
            var extra = new ColumnInfo { Id = "extra", ObjectId = "extra-guid", Code = "Extra", DataType = "int" };
            table.Columns.Add(extra);
            key.ColumnIds.Add(extra.Id);
            var diff = new PdmModelComparer().Compare(before, after);
            Assert.Contains(diff.Changes, x => x.Kind == "Table" && x.Property == "Code" && x.ChangeType == "Modified");
            Assert.Contains(diff.Changes, x => x.Kind == "Column" && x.ChangeType == "Added");
            Assert.Contains(diff.Changes, x => x.Kind == "Key" && x.Property == "Columns");
            using (var report = new StringWriter())
            {
                new PdmDiffExporter().Write(diff, PdmExportFormat.Html, report);
                Assert.Contains("RenamedTarget", report.ToString());
                Assert.DoesNotContain("<script", report.ToString());
            }
        }

        /// <summary>
        /// 验证无变化、复合键重排和 GUID 歧义。
        /// </summary>
        [Fact]
        public void DiffKeepsCompositeOrderAndReportsAmbiguity()
        {
            var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "complete.pdm");
            var before = new PdmReader().ReadFromFile(fixture);
            var after = new PdmReader().ReadFromFile(fixture);
            var comparer = new PdmModelComparer();
            Assert.Empty(comparer.Compare(before, after).Changes);
            var customer = after.AllTables.First(x => x.Code == "Customer");
            customer.Keys.First(x => x.Id == customer.PrimaryKeyId).ColumnIds.Reverse();
            Assert.Contains(comparer.Compare(before, after).Changes,
                x => x.Kind == "Key" && x.Property == "Columns");
            var duplicate = new TableInfo
            {
                Id = "duplicate",
                ObjectId = customer.ObjectId,
                Code = "Duplicate",
                Name = "Duplicate"
            };
            after.Tables.Add(duplicate);
            Assert.True(comparer.Compare(before, after).Incomplete);
        }

        /// <summary>
        /// 验证 PDM 局部 ID 变化不会成为数据库结构差异。
        /// </summary>
        [Fact]
        public void DiffUsesStableIdentityForReferenceColumns()
        {
            var before = Model();
            var after = Model();
            var reference = after.AllReferences.Single();
            var table = after.AllTables.Single(x => x.Id == reference.ParentTableId);
            var column = table.Columns.Single(x => x.Id == reference.Joins.Single().ParentColumnId);
            table.Id = "renumbered-table";
            column.Id = "renumbered-column";
            reference.ParentTableId = table.Id;
            reference.Joins.Single().ParentColumnId = column.Id;
            foreach (var key in table.Keys)
                for (var index = 0; index < key.ColumnIds.Count; index++)
                    if (key.ColumnIds[index] != "renumbered-column" &&
                        key.ColumnIds[index] == before.AllTables.Single(x =>
                            x.ObjectId == table.ObjectId).Columns.Single(x =>
                            x.ObjectId == column.ObjectId).Id)
                        key.ColumnIds[index] = column.Id;
            Assert.Empty(new PdmModelComparer().Compare(before, after).Changes);
        }

        /// <summary>
        /// 验证只出现在新模型的重复 GUID 被标记为歧义。
        /// </summary>
        [Fact]
        public void DiffReportsNewOnlyDuplicateIdentity()
        {
            var before = Model();
            var after = Model();
            after.Tables.Add(new TableInfo
            {
                Id = "first",
                ObjectId = "duplicate-new-guid",
                Code = "First"
            });
            after.Tables.Add(new TableInfo
            {
                Id = "second",
                ObjectId = "duplicate-new-guid",
                Code = "Second"
            });
            Assert.Contains(new PdmModelComparer().Compare(before, after).Changes,
                x => x.Kind == "Table" && x.ChangeType == "Ambiguous" &&
                    x.AfterValue == "duplicate-new-guid");
        }

        /// <summary>
        /// 验证索引、视图和外键变化保留完整路径。
        /// </summary>
        [Fact]
        public void DiffReportsIndexViewAndReferenceChanges()
        {
            var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "complete.pdm");
            var before = new PdmReader().ReadFromFile(fixture);
            var after = new PdmReader().ReadFromFile(fixture);
            var table = after.AllTables.First(x => x.Indexes.Count > 0);
            var index = table.Indexes.First();
            index.Unique = !index.Unique;
            index.ColumnIds.Add(table.Columns.First(x => x.Id != index.ColumnIds[0]).Id);
            var view = after.AllViews.First();
            view.ViewSQLQuery = "select Changed from Source";
            var reference = after.AllReferences.First();
            reference.ParentTableId = after.AllTables.First(x => x.Id != reference.ParentTableId).Id;
            var changes = new PdmModelComparer().Compare(before, after).Changes;
            Assert.Contains(changes, x => x.Kind == "Index" && x.Property == "Unique" &&
                x.BeforePath.Contains(table.Code + "." + index.Code));
            Assert.Contains(changes, x => x.Kind == "Index" && x.Property == "Columns");
            Assert.Contains(changes, x => x.Kind == "View" && x.Property == "SQL" &&
                x.BeforePath.Contains(view.Code));
            Assert.Contains(changes, x => x.Kind == "Reference" && x.Property == "ParentTable");
        }

        /// <summary>
        /// 验证名称兜底和显式忽略大小写。
        /// </summary>
        [Fact]
        public void DiffMatchesByNameAndCanIgnoreNameCase()
        {
            var before = Model();
            var after = Model();
            var left = before.AllTables.First();
            var right = after.AllTables.First(x => x.Id == left.Id);
            left.ObjectId = null;
            right.ObjectId = null;
            right.Id = "new-local-id";
            foreach (var reference in after.AllReferences)
            {
                if (reference.ParentTableId == left.Id) reference.ParentTableId = right.Id;
                if (reference.ChildTableId == left.Id) reference.ChildTableId = right.Id;
            }
            right.Code = right.Code.ToUpperInvariant();
            right.Name = right.Name?.ToUpperInvariant();
            Assert.Empty(new PdmModelComparer().Compare(before, after,
                new PdmCompareOptions { IgnoreCase = true }).Changes);
            Assert.Contains(new PdmModelComparer().Compare(before, after).Changes,
                x => x.Kind == "Table" && x.ChangeType == "Removed");
        }

        /// <summary>
        /// 验证原生样例的样式读取与两种渲染模式。
        /// </summary>
        [Fact]
        public void NativeFixtureRetainsStylesAndRendersOffline()
        {
            var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-visual.pdm");
            var model = new PdmReader().ReadFromFile(fixture);
            Assert.Equal(2, model.AllTables.Count());
            Assert.Empty(model.Diagnostics);
            var diagram = Assert.Single(model.AllPhysicalDiagrams);
            var table = diagram.AllSymbols.First(x => x.Kind == "TableSymbol");
            Assert.Contains("Arial,8", table.FontList);
            Assert.NotNull(table.ShadowColor);
            Assert.NotNull(table.GradientEndColor);
            using (var output = new StringWriter())
            {
                var options = new PdmDiagramRenderOptions
                {
                    Style = PdmDiagramStyle.PowerDesigner,
                    FontAvailable = _ => false
                };
                new PdmExporter().WriteDiagram(model, diagram, output, options);
                Assert.Contains("linearGradient", output.ToString());
                Assert.Contains("x2=\"1\" y2=\"1\"", output.ToString());
                Assert.Contains("Customer Name", output.ToString());
                Assert.Contains("&lt;pk,fk&gt;", output.ToString());
                Assert.Contains("FK_Order_Customer", output.ToString());
                Assert.Contains("points=\"6811,7019 6811,3999\"", output.ToString());
                Assert.Contains("M 0 0 L 900 225 L 0 450 Z", output.ToString());
                Assert.DoesNotContain(" filter=\"url(#pd-shadow)\"", output.ToString());
                Assert.DoesNotContain("http://", output.ToString().Replace("http://www.w3.org/2000/svg", ""));
                Assert.False(options.CanCompareToNative);
                Assert.Contains(options.Diagnostics, x => x.Code == "MISSING_DIAGRAM_FONT");
            }
        }

        /// <summary>
        /// 验证原生图按字段关系显示外键标记。
        /// </summary>
        [Fact]
        public void NativeFixtureRendersForeignKeyOnlyMarker()
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "native-visual.pdm"));
            var child = model.AllTables.Single(x => x.Id == "o10");
            child.Columns.Single(x => x.Id == "o14").PrimaryKey = false;
            using (var output = new StringWriter())
            {
                new PdmExporter().WriteDiagram(model, Assert.Single(model.AllPhysicalDiagrams), output,
                    new PdmDiagramRenderOptions { Style = PdmDiagramStyle.PowerDesigner });
                Assert.Contains("&lt;fk&gt;", output.ToString());
                Assert.DoesNotContain("&lt;pk,fk&gt;", output.ToString());
            }
        }

        /// <summary>
        /// 验证原生字段类型列随字段名宽度排布。
        /// </summary>
        [Fact]
        public void NativeFixturePositionsTypeColumnsByNameWidth()
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "native-visual.pdm"));
            using (var output = new StringWriter())
            {
                new PdmExporter().WriteDiagram(model, Assert.Single(model.AllPhysicalDiagrams), output,
                    new PdmDiagramRenderOptions { Style = PdmDiagramStyle.PowerDesigner });
                var svg = XDocument.Parse(output.ToString());
                XNamespace ns = "http://www.w3.org/2000/svg";
                var upper = svg.Descendants(ns + "g").Single(x => (string)x.Attribute("data-symbol-id") == "o7");
                var lower = svg.Descendants(ns + "g").Single(x => (string)x.Attribute("data-symbol-id") == "o6");
                var upperType = (int)upper.Descendants(ns + "text").Single(x => x.Value == "varchar(120)").Attribute("x");
                var lowerType = (int)lower.Descendants(ns + "text").First(x => x.Value == "int").Attribute("x");
                Assert.InRange(upperType, 6400, 6700);
                Assert.InRange(lowerType - 2086, 5100, 5400);
                Assert.True(upperType > lowerType - 2086);
            }
        }

        /// <summary>
        /// 验证 Times New Roman 字段列使用独立的宽度校准。
        /// </summary>
        [Fact]
        public void NativeTypographyFixturePositionsTypeColumn()
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "native-typography.pdm"));
            using (var output = new StringWriter())
            {
                new PdmExporter().WriteDiagram(model, Assert.Single(model.AllPhysicalDiagrams), output,
                    new PdmDiagramRenderOptions { Style = PdmDiagramStyle.PowerDesigner });
                var svg = XDocument.Parse(output.ToString());
                XNamespace ns = "http://www.w3.org/2000/svg";
                var upper = svg.Descendants(ns + "g").Single(x => (string)x.Attribute("data-symbol-id") == "o7");
                var typeX = (int)upper.Descendants(ns + "text").Single(x => x.Value == "varchar(120)").Attribute("x");
                Assert.InRange(typeX, 7700, 8000);
            }
        }

        /// <summary>
        /// 验证原生主键清单扩展表框并保持关系连线端点。
        /// </summary>
        [Fact]
        public void NativeKeysFixtureRendersKeyPane()
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "native-keys.pdm"));
            var diagram = Assert.Single(model.AllPhysicalDiagrams);
            using (var output = new StringWriter())
            {
                var options = new PdmDiagramRenderOptions
                {
                    Style = PdmDiagramStyle.PowerDesigner,
                    FontAvailable = _ => true
                };
                new PdmExporter().WriteDiagram(model, diagram, output, options);
                var svg = output.ToString();
                Assert.Contains("PK Orders", svg);
                Assert.Contains("PK Customers", svg);
                Assert.DoesNotContain(options.Diagnostics, x => x.Code == "UNSUPPORTED_DIAGRAM_STYLE");
                Assert.Contains("points=\"6811,7019 6811,4749\"", svg);
                var xml = XDocument.Parse(svg);
                XNamespace ns = "http://www.w3.org/2000/svg";
                var upper = xml.Descendants(ns + "g").Single(x => (string)x.Attribute("data-symbol-id") == "o7");
                Assert.Equal("0 0 13620 4749", (string)upper.Attribute("data-bounds"));
            }
        }

        /// <summary>
        /// 验证外部同 ID 图元不会继承本地主键清单。
        /// </summary>
        [Fact]
        public void NativeKeysFixtureDoesNotRenderLocalKeysForExternalSymbol()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-keys.pdm");
            var local = new PdmReader().ReadFromFile(path);
            var remote = new PdmReader().ReadFromFile(path);
            local.ObjectId = "local-model-guid";
            remote.ObjectId = "remote-model-guid";
            var workspace = new PdmWorkspace();
            workspace.Add("local", local);
            workspace.Add("remote", remote);
            var diagram = Assert.Single(local.AllPhysicalDiagrams);
            diagram.Symbols.Single(x => x.Id == "o7").ObjectAddress = new PdmObjectAddress
            { ModelKey = "remote", PdmId = "o9" };
            using (var output = new StringWriter())
            {
                new PdmExporter().WriteDiagram(workspace, "local", diagram, output,
                    new PdmDiagramRenderOptions { Style = PdmDiagramStyle.PowerDesigner });
                var svg = output.ToString();
                Assert.DoesNotContain("PK Orders", svg);
                Assert.Contains("PK Customers", svg);
                Assert.Contains("data-bounds=\"0 0 13620 3999\"", svg);
            }
        }

        /// <summary>
        /// 验证替代键清单和字段标记遵循原生布局。
        /// </summary>
        [Fact]
        public void NativeAlternateKeyFixtureRendersKeyPane()
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "native-alternate-key.pdm"));
            var table = model.AllTables.Single(x => x.Id == "o9");
            Assert.Equal(2, table.Keys.Count);
            Assert.Equal(new[] { "o12" }, table.Keys.Single(x => x.Id == "o204").ColumnIds);
            var options = new PdmDiagramRenderOptions
            {
                Style = PdmDiagramStyle.PowerDesigner,
                FontAvailable = _ => true
            };
            using (var output = new StringWriter())
            {
                new PdmExporter().WriteDiagram(model, Assert.Single(model.AllPhysicalDiagrams), output,
                    options);
                var svg = XDocument.Parse(output.ToString());
                XNamespace ns = "http://www.w3.org/2000/svg";
                var upper = svg.Descendants(ns + "g").Single(x =>
                    (string)x.Attribute("data-symbol-id") == "o7");
                Assert.Equal("0 0 13620 5724", (string)upper.Attribute("data-bounds"));
                Assert.Contains("AK Orders Name", upper.Value);
                Assert.Contains("<ak>", upper.Value);
                Assert.Equal(2, upper.Descendants(ns + "polyline").Count());
                Assert.Contains("points=\"6811,7507 6811,5724\"", output.ToString());
            }
            Assert.DoesNotContain(options.Diagnostics, x => x.Code == "UNSUPPORTED_DIAGRAM_STYLE");
        }

        /// <summary>
        /// 验证原生注释样例保留中文内容和独立几何。
        /// </summary>
        [Fact]
        public void NativeAnnotationFixtureRetainsRichTextAndGeometry()
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "native-annotations.pdm"));
            var diagram = Assert.Single(model.AllPhysicalDiagrams);
            var note = Assert.Single(diagram.AllSymbols.Where(x => x.Kind == "NoteSymbol"));
            Assert.Equal("你好", note.Text);
            Assert.Equal(7200, note.Rect.X1);
            Assert.Equal(-6000, note.Rect.Y1);
            Assert.Equal(12200, note.Rect.X2);
            Assert.Equal(-1800, note.Rect.Y2);
            using (var writer = new StringWriter())
            {
                new PdmExporter().WriteDiagram(model, diagram, writer,
                    new PdmDiagramRenderOptions { Style = PdmDiagramStyle.PowerDesigner });
                Assert.Contains("data-symbol-id=\"o1000\"", writer.ToString());
                Assert.Contains("M 14011 3799 H 18311 L 19011 4499", writer.ToString());
                Assert.Contains("你好", writer.ToString());
                Assert.Contains("font-size=\"1280\"", writer.ToString());
            }
        }

        /// <summary>
        /// 验证原生字体角色、字形及字号被用于表内文字。
        /// </summary>
        [Fact]
        public void NativeTypographyFixtureRendersRoleFontsAndStyles()
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "native-typography.pdm"));
            var diagram = Assert.Single(model.AllPhysicalDiagrams);
            var table = diagram.AllSymbols.First(x => x.Kind == "TableSymbol" && x.ObjectId == "o9");
            Assert.Contains("Times New Roman,12,I", table.FontList);
            table.FontList = table.FontList.Replace("STRN 0 Times New Roman,12,I",
                "STRN 0 Arial,8,N");
            using (var writer = new StringWriter())
            {
                new PdmExporter().WriteDiagram(model, diagram, writer,
                    new PdmDiagramRenderOptions { Style = PdmDiagramStyle.PowerDesigner });
                var svg = writer.ToString();
                Assert.Contains("font-family=\"Times New Roman\" font-size=\"1200\" text-anchor=\"middle\" font-style=\"italic\">Orders", svg);
                Assert.Contains("font-family=\"Times New Roman\" font-size=\"1100\" text-decoration=\"underline\">Order ID", svg);
                Assert.Contains("font-family=\"Times New Roman\" font-size=\"1100\" text-decoration=\"underline\">int", svg);
            }
        }

        /// <summary>
        /// 验证原生嵌套图元及样式可以读取和往返。
        /// </summary>
        [Fact]
        public void NativeNestedShapesRetainAreaAndChildStyles()
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "native-shapes.pdm"));
            Assert.Equal(2, model.AllTables.Count());
            var diagram = Assert.Single(model.AllPhysicalDiagrams);
            var area = Assert.Single(diagram.Symbols.Where(x => x.Kind == "ArchitectureAreaSymbol"));
            Assert.Equal("Workflow", area.Text);
            Assert.Equal("16", area.GradientFillMode);
            Assert.Equal(new[] { "EllipseSymbol", "TextSymbol", "PolylineSymbol" },
                area.SubSymbols.Select(x => x.Kind).ToArray());
            Assert.Equal("Arial,8,N", area.SubSymbols[0].FontName);
            Assert.Equal("4130", area.SubSymbols[0].TextStyle);
            Assert.Equal("7", area.SubSymbols[1].DashStyle);
            using (var writer = new StringWriter())
            {
                var options = new PdmDiagramRenderOptions
                {
                    Style = PdmDiagramStyle.PowerDesigner,
                    FontAvailable = _ => true
                };
                new PdmExporter().WriteDiagram(model, diagram, writer, options);
                var svg = writer.ToString();
                Assert.Contains("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>", svg);
                Assert.Contains("x2=\"0\" y2=\"1\"", svg);
                Assert.Contains(">Workflow</text>", svg);
                Assert.Contains(">Decision</text>", svg);
                Assert.Contains(">Review</text>", svg);
                Assert.Contains("data-symbol-id=\"o14\" points=", svg);
                var nativeOrder = new[] { "o5", "o7", "o6", "o11", "o14", "o12", "o13" };
                var positions = nativeOrder.Select(id =>
                    svg.IndexOf("data-symbol-id=\"" + id + "\"", StringComparison.Ordinal)).ToArray();
                Assert.All(positions, position => Assert.True(position >= 0));
                Assert.Equal(positions.OrderBy(position => position), positions);
                Assert.DoesNotContain(options.Diagnostics, x => x.Code == "UNSUPPORTED_DIAGRAM_STYLE");
            }
            using (var writer = new StringWriter())
            {
                new PdmExporter().Write(model, PdmExportFormat.Json, writer);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString())))
                {
                    var restored = new PdmJsonReader().Read(stream);
                    var restoredArea = Assert.Single(restored.AllPhysicalDiagrams.Single()
                        .Symbols.Where(x => x.Kind == "ArchitectureAreaSymbol"));
                    Assert.Equal("Workflow", restoredArea.Text);
                    Assert.Equal("Arial,8,N", restoredArea.SubSymbols[0].FontName);
                    Assert.Equal("7", restoredArea.SubSymbols[1].DashStyle);
                }
            }
        }

        /// <summary>
        /// 验证外键约束标签使用显式名称，并标记无法推导的名称。
        /// </summary>
        [Fact]
        public void NativeReferenceConstraintLabelRequiresStoredName()
        {
            var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-shapes.pdm");
            var missing = new PdmReader().ReadFromFile(fixture);
            var missingOptions = new PdmDiagramRenderOptions
            {
                Style = PdmDiagramStyle.PowerDesigner,
                FontAvailable = _ => true
            };
            using (var writer = new StringWriter())
                new PdmExporter().WriteDiagram(missing, missing.AllPhysicalDiagrams.Single(), writer,
                    missingOptions);
            Assert.Contains(missingOptions.Diagnostics, x =>
                x.Code == "UNRESOLVED_REFERENCE_LABEL" && x.SourceId == "o5");
            Assert.False(missingOptions.CanCompareToNative);

            var xml = File.ReadAllText(fixture).Replace("<a:Cardinality>0..*</a:Cardinality>",
                "<a:ForeignKeyConstraintName>FK_SAVED_NAME</a:ForeignKeyConstraintName>" +
                "<a:Cardinality>0..*</a:Cardinality>");
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                var model = new PdmReader().Read(stream);
                Assert.Equal("FK_SAVED_NAME", Assert.Single(model.AllReferences).ForeignKeyConstraintName);
                Assert.Contains(new PdmModelComparer().Compare(missing, model).Changes, x =>
                    x.Kind == "Reference" && x.Property == "ForeignKeyConstraintName" &&
                    x.AfterValue == "FK_SAVED_NAME");
                var options = new PdmDiagramRenderOptions
                {
                    Style = PdmDiagramStyle.PowerDesigner,
                    FontAvailable = _ => true
                };
                using (var writer = new StringWriter())
                {
                    new PdmExporter().WriteDiagram(model, model.AllPhysicalDiagrams.Single(), writer,
                        options);
                    Assert.Contains(">FK_SAVED_NAME</text>", writer.ToString());
                }
                Assert.DoesNotContain(options.Diagnostics, x => x.Code == "UNRESOLVED_REFERENCE_LABEL");
                using (var writer = new StringWriter())
                {
                    new PdmExporter().Write(model, PdmExportFormat.Json, writer);
                    using (var json = new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString())))
                        Assert.Equal("FK_SAVED_NAME", Assert.Single(new PdmJsonReader()
                            .Read(json).AllReferences).ForeignKeyConstraintName);
                }
            }
        }

        /// <summary>
        /// 验证原生关联线型及 JSON 往返。
        /// </summary>
        [Theory]
        [InlineData("native-dash", "2", "1200 600")]
        [InlineData("native-dot", "3", "225 225")]
        [InlineData("native-dashdot", "4", "600 300 150 300")]
        [InlineData("native-dashdotdot", "5", "600 300 150 300 150 300")]
        public void NativeReferenceUsesPowerDesignerDashStyle(string fixtureName, string style,
            string pattern)
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", fixtureName + ".pdm"));
            var diagram = Assert.Single(model.AllPhysicalDiagrams);
            var reference = Assert.Single(diagram.Symbols.Where(x => x.Kind == "ReferenceSymbol"));
            Assert.Equal(style, reference.DashStyle);
            using (var writer = new StringWriter())
            {
                var options = new PdmDiagramRenderOptions
                {
                    Style = PdmDiagramStyle.PowerDesigner,
                    FontAvailable = _ => true
                };
                new PdmExporter().WriteDiagram(model, diagram, writer, options);
                Assert.Contains("stroke-dasharray=\"" + pattern + "\"", writer.ToString());
                Assert.DoesNotContain(options.Diagnostics, x => x.Code == "UNSUPPORTED_DIAGRAM_STYLE");
            }
            using (var writer = new StringWriter())
            {
                new PdmExporter().Write(model, PdmExportFormat.Json, writer);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString())))
                    Assert.Equal(style, Assert.Single(new PdmJsonReader().Read(stream)
                        .AllPhysicalDiagrams.Single().Symbols.Where(x => x.Kind == "ReferenceSymbol"))
                        .DashStyle);
            }
        }

        /// <summary>
        /// 验证所有声明字体及不支持的样式都会影响原生对照资格。
        /// </summary>
        [Fact]
        public void NativeRendererReportsMissingSecondaryFontAndUnsupportedStyle()
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "native-visual.pdm"));
            var diagram = model.AllPhysicalDiagrams.Single();
            var table = diagram.AllSymbols.First(x => x.Kind == "TableSymbol");
            table.FontList = "STRN 0 Arial,8,N\nColumns 0 Missing Font,8,N";
            table.PenStyle = "99";
            var options = new PdmDiagramRenderOptions
            {
                Style = PdmDiagramStyle.PowerDesigner,
                FontAvailable = family => family == "Arial"
            };
            using (var writer = new StringWriter())
                new PdmExporter().WriteDiagram(model, diagram, writer, options);
            Assert.Contains(options.Diagnostics, x => x.SourceId == table.Id &&
                x.Code == "MISSING_DIAGRAM_FONT" && x.Message.Contains("Missing Font"));
            Assert.Contains(options.Diagnostics, x => x.SourceId == table.Id &&
                x.Code == "UNSUPPORTED_DIAGRAM_STYLE");
            Assert.False(options.CanCompareToNative);
        }

        /// <summary>
        /// 验证未实现的表内显示选项会显式阻止原生视觉对照。
        /// </summary>
        [Fact]
        public void NativeRendererReportsUnsupportedDisplayPreferences()
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "native-visual.pdm"));
            var diagram = model.AllPhysicalDiagrams.Single();
            diagram.DisplayPreferences = "[DisplayPreferences\\Object]\nTable.Keys=Yes\n" +
                "Table.Triggers=Yes\nTable.Columns._Filter=CustomFilter";
            var options = new PdmDiagramRenderOptions
            {
                Style = PdmDiagramStyle.PowerDesigner,
                FontAvailable = _ => true
            };
            using (var writer = new StringWriter())
                new PdmExporter().WriteDiagram(model, diagram, writer, options);
            Assert.DoesNotContain(options.Diagnostics, x => x.SourceId == diagram.Id &&
                x.Message.Contains("Table.Keys"));
            Assert.DoesNotContain(options.Diagnostics, x => x.SourceId == diagram.Id &&
                x.Message.Contains("Table.Triggers"));
            Assert.Contains(options.Diagnostics, x => x.SourceId == diagram.Id &&
                x.Message.Contains("CustomFilter"));
            Assert.False(options.CanCompareToNative);
        }

        /// <summary>
        /// 验证 CLI 会报告无法还原的图形样式。
        /// </summary>
        [Fact]
        public void NativeExportCliReportsStyleDiagnostics()
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            var tool = Path.Combine(root, "samples", "Bing.Pdm.Tool", "bin", "Debug", "net8.0",
                "Bing.Pdm.Tool.dll");
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "native-visual.pdm"));
            model.AllPhysicalDiagrams.Single().DisplayPreferences =
                "[DisplayPreferences\\Object]\nTable.Columns._Filter=CustomFilter";
            var directory = Path.Combine(Path.GetTempPath(), "pdm-style-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var input = Path.Combine(directory, "native.json");
                using (var writer = new StreamWriter(input))
                    new PdmExporter().Write(model, PdmExportFormat.Json, writer);
                var start = new ProcessStartInfo("dotnet")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var value in new[] { tool, "export", input,
                    Path.Combine(directory, "output"), "svg", "en", "--diagram-style", "powerdesigner" })
                    start.ArgumentList.Add(value);
                using (var process = Process.Start(start))
                {
                    process.StandardOutput.ReadToEnd();
                    var errors = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    Assert.Equal(0, process.ExitCode);
                    Assert.Contains("UNSUPPORTED_DIAGRAM_STYLE", errors);
                    Assert.Contains("CustomFilter", errors);
                }
            }
            finally { Directory.Delete(directory, true); }
        }

        /// <summary>
        /// 验证原生索引清单、索引图标、表高和关系端点。
        /// </summary>
        [Theory]
        [InlineData("native-indexes", 4749)]
        [InlineData("native-keys-indexes", 6024)]
        public void NativeIndexFixturesRenderIndexCollectionsAndReferenceGeometry(string fixtureName,
            int expectedTableHeight)
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", fixtureName + ".pdm"));
            var table = model.AllTables.Single(x => x.Id == "o9");
            var index = Assert.Single(table.Indexes);
            Assert.Equal("IX_Orders_Name", index.Code);
            Assert.Equal(new[] { "o12" }, index.ColumnIds);
            var diagram = Assert.Single(model.AllPhysicalDiagrams);
            Assert.Single(diagram.Symbols.Where(x => x.Kind == "ReferenceSymbol"));
            using (var writer = new StringWriter())
            {
                var options = new PdmDiagramRenderOptions
                {
                    Style = PdmDiagramStyle.PowerDesigner,
                    FontAvailable = _ => true
                };
                new PdmExporter().WriteDiagram(model, diagram, writer, options);
                var svg = XDocument.Parse(writer.ToString());
                XNamespace ns = "http://www.w3.org/2000/svg";
                var tableGroup = svg.Descendants(ns + "g").Single(x =>
                    (string)x.Attribute("data-symbol-id") == "o7");
                var bounds = ((string)tableGroup.Attribute("data-bounds")).Split(' ');
                Assert.Equal(expectedTableHeight, int.Parse(bounds[3]));
                Assert.Contains("IX Orders Name", svg.Root.Value);
                Assert.NotEmpty(tableGroup.Descendants(ns + "polyline"));
                var referenceLine = svg.Descendants(ns + "polyline").Single(x =>
                    (string)x.Attribute("data-symbol-id") == "o5");
                Assert.True(((string)referenceLine.Attribute("points")).Split(' ').Length >= 2);
                Assert.DoesNotContain(options.Diagnostics, x =>
                    x.Code == "UNSUPPORTED_DIAGRAM_STYLE" && x.Message.Contains("Table.Indexes"));
            }
        }

        /// <summary>
        /// 验证多个索引各占一行并维持原生表框与连线。
        /// </summary>
        [Fact]
        public void NativeMultipleIndexesRenderTwoRows()
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "native-multi-indexes.pdm"));
            var table = model.AllTables.Single(x => x.Id == "o9");
            Assert.Equal(new[] { "IX_Orders_Name", "IX_Orders_ID" },
                table.Indexes.Select(x => x.Code));
            var options = new PdmDiagramRenderOptions
            {
                Style = PdmDiagramStyle.PowerDesigner,
                FontAvailable = _ => true
            };
            using (var output = new StringWriter())
            {
                new PdmExporter().WriteDiagram(model, Assert.Single(model.AllPhysicalDiagrams),
                    output, options);
                var svg = XDocument.Parse(output.ToString());
                XNamespace ns = "http://www.w3.org/2000/svg";
                var upper = svg.Descendants(ns + "g").Single(x =>
                    (string)x.Attribute("data-symbol-id") == "o7");
                Assert.Equal("0 0 13620 5724", (string)upper.Attribute("data-bounds"));
                Assert.Contains("IX Orders Name", upper.Value);
                Assert.Contains("IX Orders ID", upper.Value);
                Assert.Equal(6, upper.Descendants(ns + "polyline").Count());
                Assert.Contains("points=\"6811,7507 6811,5724\"", output.ToString());
            }
            Assert.DoesNotContain(options.Diagnostics, x =>
                x.Code == "UNSUPPORTED_DIAGRAM_STYLE" && x.Message.Contains("Table.Indexes"));
        }

        /// <summary>
        /// 验证模型显示配置控制列清单与字段内容。
        /// </summary>
        [Fact]
        public void NativeDisplayPreferencesControlTableColumns()
        {
            var model = new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "native-visual.pdm"));
            var diagram = model.AllPhysicalDiagrams.Single();
            Assert.Contains("Table.Columns=Yes", diagram.DisplayPreferences);
            diagram.DisplayPreferences = "[DisplayPreferences\\Object]\nTable.DisplayName=Yes\n" +
                "Table.Columns=Yes\nTable.Columns._Columns=KeyIndicator\nTable.Columns._Limit=1";
            using (var writer = new StringWriter())
            {
                new PdmExporter().WriteDiagram(model, model.AllPhysicalDiagrams.Single(), writer,
                    new PdmDiagramRenderOptions { Style = PdmDiagramStyle.PowerDesigner });
                Assert.Contains("Order ID", writer.ToString());
                Assert.DoesNotContain("Customer Name", writer.ToString());
                Assert.DoesNotContain("varchar(120)", writer.ToString());
            }
        }

        /// <summary>
        /// 验证 RTF 混合样式保留且纯文本一致。
        /// </summary>
        [Fact]
        public void RichTextSegmentsPreserveBoldItalicAndPlainText()
        {
            var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "real-compat.pdm");
            var xml = File.ReadAllText(fixture).Replace(@"{\rtf1\ansi\ansicpg936 \'c4\'e3\'ba\'c3}",
                @"{\rtf1\ansi A\b B\b0 C\i D\i0}");
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                var model = new PdmReader().Read(stream);
                var note = model.AllPhysicalDiagrams.SelectMany(x => x.AllSymbols)
                    .First(x => x.Kind == "NoteSymbol");
                Assert.Equal("ABCD", note.Text);
                Assert.Equal(note.Text, string.Concat(note.RichTextSegments.Select(x => x.Text)));
                Assert.Contains(note.RichTextSegments, x => x.Bold && x.Text.Contains("B"));
                Assert.Contains(note.RichTextSegments, x => x.Italic && x.Text.Contains("D"));
                using (var writer = new StringWriter())
                {
                    new PdmExporter().WriteDiagram(model, model.AllPhysicalDiagrams.Single(), writer,
                        new PdmDiagramRenderOptions { Style = PdmDiagramStyle.PowerDesigner });
                    Assert.Contains("font-weight=\"bold\"", writer.ToString());
                    Assert.Contains("font-style=\"italic\"", writer.ToString());
                }
            }
        }

        /// <summary>
        /// 验证混合输入的差异命令及变更退出码。
        /// </summary>
        [Fact]
        public void DiffCliSupportsPdmJsonAndFailOnChange()
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            var tool = Path.Combine(root, "samples", "Bing.Pdm.Tool", "bin", "Debug", "net8.0", "Bing.Pdm.Tool.dll");
            var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-visual.pdm");
            var directory = Path.Combine(Path.GetTempPath(), "pdm-diff-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var model = new PdmReader().ReadFromFile(fixture);
                var json = Path.Combine(directory, "after.json");
                using (var writer = new StreamWriter(json))
                    new PdmExporter().Write(model, PdmExportFormat.Json, writer);
                Assert.Equal(0, Run(tool, "diff", fixture, json, Path.Combine(directory, "same"), "json", "--fail-on-change"));
                model.AllTables.First().Columns.First().DataType = "bigint";
                using (var writer = new StreamWriter(json))
                    new PdmExporter().Write(model, PdmExportFormat.Json, writer);
                Assert.Equal(3, Run(tool, "diff", fixture, json, Path.Combine(directory, "changed"), "json", "--fail-on-change"));
                Assert.Contains("DataType", File.ReadAllText(Path.Combine(directory, "changed", "native-visual-to-after.json")));
                Assert.Equal(2, Run(tool, "diff", fixture, json, Path.Combine(directory, "invalid"), "svg"));
                Assert.Equal(1, Run(tool, "diff", Path.Combine(directory, "unknown.txt"), json,
                    Path.Combine(directory, "bad-input"), "json"));
            }
            finally { Directory.Delete(directory, true); }
        }

        /// <summary>
        /// 运行 CLI 并获取退出码。
        /// </summary>
        private static int Run(string tool, params string[] arguments)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add(tool);
            foreach (var value in arguments) start.ArgumentList.Add(value);
            using (var process = Process.Start(start))
            {
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        /// <summary>
        /// 读取 Office 包中的 XML 项。
        /// </summary>
        private static string ReadEntry(ZipArchiveEntry entry)
        {
            using (var reader = new StreamReader(entry.Open()))
                return reader.ReadToEnd();
        }
    }
}
