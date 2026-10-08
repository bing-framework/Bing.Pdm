using System;
using System.IO;
using System.Linq;
using Bing.Pdm.Models;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Reader;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证配对身份在结构关系中的一致性。
    /// </summary>
    public sealed class PdmComparisonIdentityTests
    {
        /// <summary>
        /// 读取完整结构样例。
        /// </summary>
        private static PdmInfo ReadModel() => new PdmReader().ReadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "complete.pdm"));

        /// <summary>
        /// 更新结构对象的 GUID。
        /// </summary>
        private static void RegenerateGuids(PdmInfo model)
        {
            foreach (var table in model.AllTables)
            {
                table.ObjectId = "new-table-" + table.Id;
                foreach (var column in table.Columns) column.ObjectId = "new-column-" + column.Id;
                foreach (var key in table.Keys) key.ObjectId = "new-key-" + key.Id;
                foreach (var index in table.Indexes) index.ObjectId = "new-index-" + index.Id;
            }
            foreach (var reference in model.AllReferences) reference.ObjectId = "new-reference-" + reference.Id;
            foreach (var view in model.AllViews) view.ObjectId = "new-view-" + view.Id;
        }

        /// <summary>
        /// 验证 GUID 重建不改变名称匹配的结构。
        /// </summary>
        [Fact]
        public void RegeneratedGuidsPreserveColumnOrderKeysIndexesAndReferences()
        {
            var before = ReadModel();
            var after = ReadModel();
            RegenerateGuids(after);
            var comparer = new PdmModelComparer();
            Assert.Empty(comparer.Compare(before, after).Changes);
            Assert.Empty(comparer.Compare(after, before).Changes);
            Assert.Empty(comparer.Compare(before, before).Changes);
        }

        /// <summary>
        /// 验证忽略名称大小写同样适用于关系配对。
        /// </summary>
        [Fact]
        public void CaseInsensitiveFallbackPreservesRelationships()
        {
            var before = ReadModel();
            var after = ReadModel();
            RegenerateGuids(after);
            foreach (var table in after.AllTables)
            {
                table.Name = table.Name?.ToUpperInvariant();
                table.Code = table.Code?.ToUpperInvariant();
                foreach (var column in table.Columns)
                {
                    column.Name = column.Name?.ToUpperInvariant();
                    column.Code = column.Code?.ToUpperInvariant();
                }
            }
            Assert.Empty(new PdmModelComparer().Compare(before, after,
                new PdmCompareOptions { IgnoreCase = true }).Changes);
        }

        /// <summary>
        /// 验证名称配对仍能检测复合键和索引重排。
        /// </summary>
        [Fact]
        public void FallbackStillDetectsOrderedRelationshipChanges()
        {
            var before = ReadModel();
            var after = ReadModel();
            RegenerateGuids(after);
            var table = after.AllTables.First(x => x.Keys.Any(k => k.ColumnIds.Count > 1));
            table.Keys.First(x => x.ColumnIds.Count > 1).ColumnIds.Reverse();
            var index = after.AllTables.SelectMany(x => x.Indexes).First();
            var owner = after.AllTables.First(x => x.Indexes.Contains(index));
            index.ColumnIds.Add(owner.Columns.First(x => !index.ColumnIds.Contains(x.Id)).Id);
            var diff = new PdmModelComparer().Compare(before, after);
            Assert.Contains(diff.Changes, x => x.Kind == "Key" && x.Property == "Columns");
            Assert.Contains(diff.Changes, x => x.Kind == "Index" && x.Property == "Columns");
            Assert.DoesNotContain(diff.Changes, x => x.Property == "Joins" || x.Property == "ColumnOrder");
        }

        /// <summary>
        /// 验证列重命名不误报关系改变。
        /// </summary>
        [Fact]
        public void GuidMatchedColumnRenameKeepsRelationships()
        {
            var before = ReadModel();
            var after = ReadModel();
            var column = after.AllTables.SelectMany(x => x.Columns).First(x => x.ObjectId != null);
            column.Code += "Renamed";
            var diff = new PdmModelComparer().Compare(before, after);
            Assert.Contains(diff.Changes, x => x.Kind == "Column" && x.Property == "Code");
            Assert.DoesNotContain(diff.Changes, x => x.Property == "Columns" || x.Property == "PrimaryKey"
                || x.Property == "ColumnOrder" || x.Property == "Joins");
        }

        /// <summary>
        /// 验证本模型限定地址复用名称匹配结果。
        /// </summary>
        [Fact]
        public void LocalWorkspaceAddressesUseMatchedObjects()
        {
            var before = ReadModel();
            var after = ReadModel();
            RegenerateGuids(after);
            foreach (var model in new[] { before, after })
                foreach (var reference in model.AllReferences)
                {
                    var parent = model.AllTables.FirstOrDefault(x => x.Id == reference.ParentTableId);
                    if (parent != null) reference.ParentTableAddress = new PdmObjectAddress
                    { ModelKey = "current", PdmId = parent.Id, ObjectId = parent.ObjectId };
                    foreach (var join in reference.Joins)
                    {
                        var column = model.AllTables.SelectMany(x => x.Columns)
                            .FirstOrDefault(x => x.Id == join.ParentColumnId);
                        if (column != null) join.ParentColumnAddress = new PdmObjectAddress
                        { ModelKey = "current", PdmId = column.Id, ObjectId = column.ObjectId };
                    }
                }
            Assert.Empty(new PdmModelComparer().Compare(before, after).Changes);
        }

        /// <summary>
        /// 创建包含同名引用的嵌套包模型。
        /// </summary>
        private static PdmInfo PackageModel(bool newGuids)
        {
            var model = new PdmInfo { Id = "model" };
            var table = new TableInfo { Id = "t", Code = "Target", PrimaryKeyId = "k" };
            table.Columns.Add(new ColumnInfo { Id = "c", Code = "Id" });
            table.Keys.Add(new Bing.Pdm.Models.Keys.KeyInfo { Id = "k", Code = "PK", ColumnIds = { "c" } });
            model.Tables.Add(table);
            var outer = new PackageInfo { Id = "outer", Code = "Outer" };
            model.Packages.Add(outer);
            foreach (var code in new[] { "A", "B" })
            {
                var package = new PackageInfo { Id = code, Code = code };
                package.References.Add(new ReferenceInfo
                {
                    Id = "ref-" + code,
                    Code = "SameName",
                    ObjectId = (newGuids ? "new-" : "old-") + code,
                    ParentTableId = "t",
                    ChildTableId = "t",
                    ParentKeyId = "k"
                });
                outer.Packages.Add(package);
            }
            return model;
        }

        /// <summary>
        /// 验证不同包的同名引用分别匹配。
        /// </summary>
        [Fact]
        public void ReferencesMatchByTheirNestedPackagePath()
        {
            var diff = new PdmModelComparer().Compare(PackageModel(false), PackageModel(true));
            Assert.Empty(diff.Changes);
            Assert.False(diff.Incomplete);
        }

        /// <summary>
        /// 验证保持 GUID 的引用移动保留两侧包路径。
        /// </summary>
        [Fact]
        public void GuidMatchedReferenceMoveReportsPackagePath()
        {
            var before = PackageModel(false);
            var after = PackageModel(false);
            var packages = after.Packages.Single().Packages;
            var reference = packages[0].References.Single();
            packages[0].References.Remove(reference);
            packages[1].References.Add(reference);
            var change = Assert.Single(new PdmModelComparer().Compare(before, after).Changes);
            Assert.Equal("Path", change.Property);
            Assert.Equal("Outer/A/SameName", change.BeforePath);
            Assert.Equal("Outer/B/SameName", change.AfterPath);
        }
    }
}
