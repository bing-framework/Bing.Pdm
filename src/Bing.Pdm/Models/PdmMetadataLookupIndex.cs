using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Pdm.Models.Others;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 复制关系的统一查询结果。
    /// </summary>
    public sealed class PdmReplicationLinkInfo
    {
        /// <summary>
        /// 获取或设置复制关系的 PDM ID。
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// 获取或设置来源对象 GUID。
        /// </summary>
        public string OriginalId { get; set; }
        /// <summary>
        /// 获取或设置副本对象的 PDM ID。
        /// </summary>
        public string ReplicaObjectRef { get; set; }
        /// <summary>
        /// 获取或设置父复制关系的 PDM ID。
        /// </summary>
        public string ParentReplicationId { get; set; }
    }

    /// <summary>
    /// 一条复制来源链的查询结果。
    /// </summary>
    public sealed class PdmLineageResult
    {
        /// <summary>
        /// 获取从副本向来源排列的复制关系。
        /// </summary>
        public List<PdmReplicationLinkInfo> Links { get; } = new List<PdmReplicationLinkInfo>();
        /// <summary>
        /// 获取链是否遇到循环。
        /// </summary>
        public bool CycleDetected { get; internal set; }
        /// <summary>
        /// 获取来源链是否存在多个候选。
        /// </summary>
        public bool Ambiguous { get; internal set; }
        /// <summary>
        /// 获取来源对象是否无法定位。
        /// </summary>
        public bool UnresolvedSource { get; internal set; }
    }

    /// <summary>
    /// 为复制元数据提供独立于业务对象的查找索引。
    /// </summary>
    public sealed class PdmMetadataLookupIndex
    {
        /// <summary>
        /// 按 PDM ID 索引目标模型。
        /// </summary>
        private readonly Dictionary<string, TargetModelInfo> _targets = new Dictionary<string, TargetModelInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按 PDM ID 索引复制关系。
        /// </summary>
        private readonly Dictionary<string, PdmReplicationInfo> _replications = new Dictionary<string, PdmReplicationInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按 PDM ID 索引子复制关系。
        /// </summary>
        private readonly Dictionary<string, PdmSubReplicationInfo> _children = new Dictionary<string, PdmSubReplicationInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按 PDM ID 索引嵌入对象。
        /// </summary>
        private readonly Dictionary<string, PdmEmbeddedObjectInfo> _embedded = new Dictionary<string, PdmEmbeddedObjectInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按对象 GUID 索引当前文档中的对象 ID。
        /// </summary>
        private readonly Dictionary<string, List<string>> _objectIds =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// 保存复制关系的统一查询视图。
        /// </summary>
        private readonly List<PdmReplicationLinkInfo> _links = new List<PdmReplicationLinkInfo>();

        /// <summary>
        /// 初始化一个 <see cref="PdmMetadataLookupIndex"/> 类型的实例。
        /// </summary>
        /// <param name="model">用于建立索引的 PDM 模型。</param>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> 为 <see langword="null"/>。</exception>
        public PdmMetadataLookupIndex(PdmInfo model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            foreach (var target in model.AllTargetModels)
            {
                Add(target.Id, target, _targets);
                foreach (var embedded in target.EmbeddedObjects)
                {
                    Add(embedded.Id, embedded, _embedded);
                    AddObjectId(embedded.ObjectId, embedded.Id);
                }
            }
            foreach (var table in model.AllTables)
            {
                AddObjectId(table.ObjectId, table.Id);
                foreach (var column in table.Columns) AddObjectId(column.ObjectId, column.Id);
                foreach (var key in table.Keys) AddObjectId(key.ObjectId, key.Id);
                foreach (var trigger in table.Triggers) AddObjectId(trigger.ObjectId, trigger.Id);
                foreach (var index in table.Indexes)
                {
                    AddObjectId(index.ObjectId, index.Id);
                    foreach (var column in index.IndexColumns) AddObjectId(column.ObjectId, column.Id);
                }
            }
            AddObjectId(model.ObjectId, model.Id);
            foreach (var package in Packages(model.Packages)) AddObjectId(package.ObjectId, package.Id);
            foreach (var view in model.AllViews)
            {
                AddObjectId(view.ObjectId, view.Id);
                foreach (var column in view.Columns) AddObjectId(column.ObjectId, column.Id);
            }
            foreach (var reference in model.AllReferences)
            {
                AddObjectId(reference.ObjectId, reference.Id);
                foreach (var join in reference.Joins) AddObjectId(join.ObjectId, join.Id);
            }
            foreach (var shortcut in model.AllShortcuts) AddObjectId(shortcut.ObjectId, shortcut.Id);
            foreach (var replication in model.AllReplications)
            {
                Add(replication.Id, replication, _replications);
                _links.Add(new PdmReplicationLinkInfo
                {
                    Id = replication.Id, OriginalId = replication.OriginalId,
                    ReplicaObjectRef = replication.ReplicaObjectRef
                });
            }
            foreach (var child in model.AllSubReplications)
            {
                Add(child.Id, child, _children);
                _links.Add(new PdmReplicationLinkInfo
                {
                    Id = child.Id, OriginalId = child.OriginalId,
                    ReplicaObjectRef = child.ReplicaObjectRef, ParentReplicationId = child.ParentReplicationId
                });
            }
        }

        /// <summary>
        /// 按 PDM ID 查找目标模型。
        /// </summary>
        /// <param name="id">目标模型的 PDM ID。</param>
        /// <param name="value">找到的目标模型。</param>
        /// <returns>找到目标模型时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        public bool TryGetTargetModel(string id, out TargetModelInfo value) =>
            _targets.TryGetValue(id ?? string.Empty, out value);

        /// <summary>
        /// 按 PDM ID 查找复制关系。
        /// </summary>
        /// <param name="id">复制关系的 PDM ID。</param>
        /// <param name="value">找到的复制关系。</param>
        /// <returns>找到复制关系时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        public bool TryGetReplication(string id, out PdmReplicationInfo value) =>
            _replications.TryGetValue(id ?? string.Empty, out value);

        /// <summary>
        /// 按 PDM ID 查找子复制关系。
        /// </summary>
        /// <param name="id">子复制关系的 PDM ID。</param>
        /// <param name="value">找到的子复制关系。</param>
        /// <returns>找到子复制关系时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        public bool TryGetSubReplication(string id, out PdmSubReplicationInfo value) =>
            _children.TryGetValue(id ?? string.Empty, out value);

        /// <summary>
        /// 按 PDM ID 查找嵌入对象。
        /// </summary>
        /// <param name="id">嵌入对象的 PDM ID。</param>
        /// <param name="value">找到的嵌入对象。</param>
        /// <returns>找到嵌入对象时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        public bool TryGetEmbeddedObject(string id, out PdmEmbeddedObjectInfo value) =>
            _embedded.TryGetValue(id ?? string.Empty, out value);

        /// <summary>
        /// 按副本对象 ID 查找复制来源。
        /// </summary>
        /// <param name="id">副本对象的 PDM ID。</param>
        /// <returns>匹配副本对象 ID 的复制关系。</returns>
        public IEnumerable<PdmReplicationLinkInfo> FindByReplicaRef(string id) =>
            _links.Where(x => x.ReplicaObjectRef == id);

        /// <summary>
        /// 按来源对象 GUID 查找副本。
        /// </summary>
        /// <param name="id">来源对象的 GUID。</param>
        /// <returns>匹配来源对象 GUID 的复制关系。</returns>
        public IEnumerable<PdmReplicationLinkInfo> FindByOriginalId(string id) =>
            _links.Where(x => PdmIdentity.Equals(x.OriginalId, id));

        /// <summary>
        /// 获取指定复制关系的子对象映射。
        /// </summary>
        /// <param name="id">父复制关系的 PDM ID。</param>
        /// <returns>属于指定复制关系的子复制关系。</returns>
        public IEnumerable<PdmSubReplicationInfo> GetSubReplications(string id) =>
            _children.Values.Where(x => x.ParentReplicationId == id);

        /// <summary>
        /// 从副本对象向上追溯唯一的来源链。
        /// </summary>
        /// <param name="replicaRef">副本对象的 PDM ID。</param>
        /// <returns>来源链及循环、歧义或未解析状态。</returns>
        public PdmLineageResult TraceOrigins(string replicaRef)
        {
            var result = new PdmLineageResult();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = replicaRef;
            while (!string.IsNullOrEmpty(current))
            {
                if (!visited.Add(current))
                {
                    result.CycleDetected = true;
                    break;
                }
                var matches = FindByReplicaRef(current).ToArray();
                if (matches.Length > 1)
                {
                    result.Ambiguous = true;
                    break;
                }
                if (matches.Length == 0) break;
                var link = matches[0];
                result.Links.Add(link);
                if (string.IsNullOrEmpty(link.OriginalId)) break;
                if (!_objectIds.TryGetValue(PdmIdentity.Normalize(link.OriginalId), out var origins) || origins.Count == 0)
                {
                    result.UnresolvedSource = true;
                    break;
                }
                if (origins.Count > 1)
                {
                    result.Ambiguous = true;
                    break;
                }
                current = origins[0];
            }
            return result;
        }

        /// <summary>
        /// 将非空标识添加到索引。
        /// </summary>
        private static void Add<T>(string id, T value, Dictionary<string, T> target)
        {
            if (!string.IsNullOrEmpty(id) && !target.ContainsKey(id)) target.Add(id, value);
        }

        /// <summary>
        /// 将对象 GUID 映射为 PDM ID。
        /// </summary>
        private void AddObjectId(string guid, string id)
        {
            if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(id)) return;
            guid = PdmIdentity.Normalize(guid);
            if (!_objectIds.TryGetValue(guid, out var ids))
            {
                ids = new List<string>();
                _objectIds.Add(guid, ids);
            }
            if (!ids.Contains(id)) ids.Add(id);
        }

        /// <summary>
        /// 递归枚举包。
        /// </summary>
        private static IEnumerable<PackageInfo> Packages(IEnumerable<PackageInfo> roots)
        {
            foreach (var package in roots)
            {
                yield return package;
                foreach (var child in Packages(package.Packages)) yield return child;
            }
        }
    }
}
