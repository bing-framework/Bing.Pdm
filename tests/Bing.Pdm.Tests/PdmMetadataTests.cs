using System;
using System.IO;
using System.Linq;
using System.Text;
using Bing.Pdm;
using Bing.Pdm.Reader;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证目标模型和复制关系读取。
    /// </summary>
    public sealed class PdmMetadataTests
    {
        /// <summary>
        /// 获取脱敏兼容 fixture。
        /// </summary>
        private static string Fixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "real-compat.pdm");

        /// <summary>
        /// 验证会话引用、嵌入对象、复制来源及 JSON 往返。
        /// </summary>
        [Fact]
        public void ReadsLineageWithoutAddingEmbeddedBusinessTables()
        {
            var model = ReadWithMetadata(@"
<pdmc:TargetModels><pdmo:TargetModel Id=""target-model1"">
<pdma:ObjectID>target-model-guid</pdma:ObjectID><pdma:TargetModelID>external-model-guid</pdma:TargetModelID>
<pdma:TargetModelURL>external.pdm</pdma:TargetModelURL><pdma:TargetModelClassID>MODEL</pdma:TargetModelClassID>
<pdmc:SessionShortcuts><pdmo:Shortcut Ref=""shortcut-target""/></pdmc:SessionShortcuts>
<pdmc:SessionReplications><pdmo:Replication Ref=""replication1""/><pdmo:SubReplication Ref=""sub1""/></pdmc:SessionReplications>
<pdmc:FullShortcutModel><pdmo:Model Id=""embedded1""><pdma:ObjectID>embedded-guid</pdma:ObjectID>
<pdmc:Tables><pdmo:Shortcut Id=""embedded-shortcut""><pdma:ObjectID>embedded-shortcut-guid</pdma:ObjectID>
<pdma:TargetID>target-guid</pdma:TargetID></pdmo:Shortcut></pdmc:Tables>
</pdmo:Model></pdmc:FullShortcutModel></pdmo:TargetModel></pdmc:TargetModels>
<pdmc:Replications><pdmo:Replication Id=""replication1""><pdma:ObjectID>replication-guid</pdma:ObjectID>
<pdma:OriginalID>target-guid</pdma:OriginalID><pdma:OriginalClassID>TABLE</pdma:OriginalClassID>
<pdmc:ReplicaObject><pdmo:Shortcut Ref=""shortcut-target""/></pdmc:ReplicaObject>
<pdmc:SubReplications><pdmo:SubReplication Id=""sub1""><pdma:OriginalID>child-guid</pdma:OriginalID>
<pdmc:ReplicaObject><pdmo:Table Ref=""child-table""/></pdmc:ReplicaObject></pdmo:SubReplication>
</pdmc:SubReplications></pdmo:Replication></pdmc:Replications>");
            Assert.Equal(2, model.AllTables.Count());
            Assert.True(model.MetadataLookup.TryGetTargetModel("target-model1", out var target));
            Assert.Equal("external-model-guid", target.TargetModelId);
            Assert.Equal(new[] { "shortcut-target" }, target.SessionShortcutRefs);
            Assert.Equal(new[] { "replication1", "sub1" }, target.SessionReplicationRefs);
            Assert.True(model.MetadataLookup.TryGetEmbeddedObject("embedded-shortcut", out var embedded));
            Assert.Equal("target-guid", embedded.TargetId);
            Assert.True(model.MetadataLookup.TryGetReplication("replication1", out var replication));
            Assert.Equal("shortcut-target", replication.ReplicaObjectRef);
            Assert.True(model.MetadataLookup.TryGetSubReplication("sub1", out var child));
            Assert.Equal("replication1", child.ParentReplicationId);
            Assert.Single(model.MetadataLookup.GetSubReplications("replication1"));
            Assert.Single(model.MetadataLookup.FindByReplicaRef("shortcut-target"));
            Assert.Single(model.MetadataLookup.FindByOriginalId("target-guid"));
            Assert.Single(model.MetadataLookup.TraceOrigins("shortcut-target").Links);
            Assert.DoesNotContain(model.Diagnostics, x => x.Code.StartsWith("UNRESOLVED_REPLICA"));

            using (var writer = new StringWriter())
            {
                new PdmExporter().Write(model, PdmExportFormat.Json, writer);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString())))
                {
                    var restored = new PdmJsonReader().Read(stream);
                    Assert.Equal(2, restored.AllTables.Count());
                    Assert.True(restored.MetadataLookup.TryGetEmbeddedObject("embedded-shortcut", out _));
                    Assert.Equal(model.AllSubReplications.Count(), restored.AllSubReplications.Count());
                    Assert.Equal(model.Diagnostics.Count, restored.Diagnostics.Count);
                }
            }
        }

        /// <summary>
        /// 验证缺失来源、副本和会话引用分别给出诊断。
        /// </summary>
        [Fact]
        public void DiagnosesMissingLineageTargets()
        {
            var model = ReadWithMetadata(@"
<pdmc:TargetModels><pdmo:TargetModel Id=""missing-target""><pdmc:SessionReplications>
<pdmo:Replication Ref=""absent-replication""/></pdmc:SessionReplications></pdmo:TargetModel></pdmc:TargetModels>
<pdmc:Replications><pdmo:Replication Id=""missing-replication"">
<pdma:OriginalID>absent-guid</pdma:OriginalID><pdmc:ReplicaObject>
<pdmo:Table Ref=""absent-table""/></pdmc:ReplicaObject>
</pdmo:Replication></pdmc:Replications>");
            Assert.Contains(model.Diagnostics, x => x.Code == "UNRESOLVED_SESSION_REF");
            Assert.Contains(model.Diagnostics, x => x.Code == "UNRESOLVED_REPLICA_OBJECT");
            Assert.Contains(model.Diagnostics, x => x.Code == "UNRESOLVED_REPLICATION_ORIGIN");
        }

        /// <summary>
        /// 验证引用型子复制在定义读取后关联。
        /// </summary>
        [Fact]
        public void BindsReferencedSubReplicationAndReplicaDefinition()
        {
            var model = ReadWithMetadata(@"
<pdmc:Replications><pdmo:Replication Id=""parent-replication"">
<pdmc:ReplicaObject><pdmo:Table Id=""replica-definition""/></pdmc:ReplicaObject>
<pdmc:SubReplications><pdmo:SubReplication Ref=""referenced-child""/>
<pdmo:SubReplication Ref=""missing-child""/></pdmc:SubReplications>
</pdmo:Replication></pdmc:Replications>
<pdmc:SubReplications><pdmo:SubReplication Id=""referenced-child"">
<pdma:OriginalID>target-guid</pdma:OriginalID>
<pdmc:ReplicaObject><pdmo:Table Ref=""target-table""/></pdmc:ReplicaObject>
</pdmo:SubReplication></pdmc:SubReplications>");
            Assert.True(model.MetadataLookup.TryGetReplication("parent-replication", out var parent));
            Assert.Equal("replica-definition", parent.ReplicaObjectRef);
            Assert.Equal(new[] { "referenced-child", "missing-child" }, parent.SubReplicationRefs);
            var child = Assert.Single(model.MetadataLookup.GetSubReplications(parent.Id));
            Assert.Equal("referenced-child", child.Id);
            Assert.Contains(model.Diagnostics, x => x.Code == "UNRESOLVED_SUB_REPLICATION" &&
                x.SourceId == parent.Id);
        }

        /// <summary>
        /// 验证重复来源 GUID 不会被任意选取。
        /// </summary>
        [Fact]
        public void ReportsAmbiguousLineageOrigin()
        {
            var model = ReadWithMetadata(@"
<pdmc:Replications><pdmo:Replication Id=""ambiguous-replication"">
<pdma:OriginalID>target-guid</pdma:OriginalID>
<pdmc:ReplicaObject><pdmo:Table Ref=""child-table""/></pdmc:ReplicaObject>
</pdmo:Replication></pdmc:Replications>
<pdmc:TargetModels><pdmo:TargetModel Id=""embedded-target"">
<pdmc:FullShortcutModel><pdmo:Model Id=""embedded-model"">
<pdmc:Tables><pdmo:Table Id=""embedded-table"">
<pdma:ObjectID>target-guid</pdma:ObjectID>
</pdmo:Table></pdmc:Tables></pdmo:Model></pdmc:FullShortcutModel>
</pdmo:TargetModel></pdmc:TargetModels>");
            var lineage = model.MetadataLookup.TraceOrigins("child-table");
            Assert.Single(lineage.Links);
            Assert.True(lineage.Ambiguous);
            Assert.False(lineage.CycleDetected);
        }

        /// <summary>
        /// 将元数据插入脱敏 PDM 并读取。
        /// </summary>
        private static Bing.Pdm.Models.PdmInfo ReadWithMetadata(string metadata)
        {
            var xml = File.ReadAllText(Fixture).Replace("</pdmo:Model>", metadata + "</pdmo:Model>");
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
                return new PdmReader().Read(stream);
        }
    }
}
