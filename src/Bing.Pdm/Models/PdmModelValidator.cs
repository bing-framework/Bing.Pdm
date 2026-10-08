using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bing.Pdm.Models.Others;
using Bing.Pdm.Models.PhysicalDiagrams;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;
using Newtonsoft.Json.Serialization;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 一项模型校验问题。
    /// </summary>
    public sealed class PdmValidationIssue
    {
        /// <summary>
        /// 获取或设置问题代码。
        /// </summary>
        public string Code { get; set; }
        /// <summary>
        /// 获取或设置对象种类。
        /// </summary>
        public string Kind { get; set; }
        /// <summary>
        /// 获取或设置来源标识。
        /// </summary>
        public string SourceId { get; set; }
        /// <summary>
        /// 获取或设置对象路径。
        /// </summary>
        public string Path { get; set; }
        /// <summary>
        /// 获取或设置目标标识。
        /// </summary>
        public string TargetId { get; set; }
        /// <summary>
        /// 获取或设置引用角色。
        /// </summary>
        public string Role { get; set; }
        /// <summary>
        /// 获取或设置问题范围。
        /// </summary>
        /// <remarks>范围为 Structure、Diagram 或 Metadata。</remarks>
        public string Scope { get; set; }
    }

    /// <summary>
    /// 模型校验结果。
    /// </summary>
    public sealed class PdmValidationResult
    {
        /// <summary>
        /// 获取校验问题集合。
        /// </summary>
        public List<PdmValidationIssue> Issues { get; } = new List<PdmValidationIssue>();
        /// <summary>
        /// 获取模型是否通过校验。
        /// </summary>
        public bool IsValid => Issues.Count == 0;
    }

    /// <summary>
    /// 校验当前模型的对象身份与引用。
    /// </summary>
    /// <remarks>直接检查集合，不重建索引、不解析 Shortcut，也不修改模型诊断。</remarks>
    public sealed class PdmModelValidator
    {
        /// <summary>
        /// 校验当前模型的对象身份与引用。
        /// </summary>
        /// <param name="model">待校验的模型。</param>
        /// <param name="workspace">显式依赖工作区；缺失时外部地址标记为未验证。</param>
        /// <param name="modelKey">当前模型在工作区中的键。</param>
        /// <returns>包含校验问题的结果；没有问题时 <see cref="PdmValidationResult.IsValid"/> 为 <see langword="true"/>。</returns>
        public PdmValidationResult Validate(PdmInfo model, PdmWorkspace workspace = null, string modelKey = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (workspace != null)
            {
                if (modelKey == null)
                    modelKey = workspace.Models.Where(x => ReferenceEquals(x.Value, model)).Select(x => x.Key).SingleOrDefault();
                if (modelKey == null || !workspace.TryGetModel(modelKey, out var current) || !ReferenceEquals(current, model))
                    throw new ArgumentException("The validation model must be the named workspace model.", nameof(modelKey));
            }
            var inventory = new Inventory(model);
            var result = new PdmValidationResult();
            result.Issues.AddRange(inventory.Issues);
            var context = new ValidationContext(inventory, workspace, modelKey, result);
            context.Validate();
            result.Issues.Sort((a, b) => string.CompareOrdinal(
                string.Join("\u001f", a.Scope, a.Path, a.Code, a.Role, a.TargetId),
                string.Join("\u001f", b.Scope, b.Path, b.Code, b.Role, b.TargetId)));
            return result;
        }

        /// <summary>
        /// 当前模型中的对象定义。
        /// </summary>
        private sealed class Entry
        {
            /// <summary>
            /// 获取或设置对象实例。
            /// </summary>
            public object Value;
            /// <summary>
            /// 获取或设置直接所属对象。
            /// </summary>
            public object Parent;
            /// <summary>
            /// 获取或设置物理图范围。
            /// </summary>
            public PhysicalDiagramInfo Diagram;
            /// <summary>
            /// 获取或设置局部身份范围。
            /// </summary>
            public object IdentityScope;
            /// <summary>
            /// 获取或设置对象路径。
            /// </summary>
            public string Path;
            /// <summary>
            /// 获取或设置对象种类。
            /// </summary>
            public string Kind;
            /// <summary>
            /// 获取或设置校验范围。
            /// </summary>
            public string Scope;
            /// <summary>
            /// 获取对象局部标识。
            /// </summary>
            public string Id => (Value as PdmCommonInfo)?.Id ?? (Value as DiagramSymbolInfo)?.Id;
            /// <summary>
            /// 获取对象 GUID。
            /// </summary>
            public string Guid => (Value as PdmCommonInfo)?.ObjectId;
        }

        /// <summary>
        /// 独立于 Lookup 的当前对象清单。
        /// </summary>
        private sealed class Inventory
        {
            /// <summary>
            /// 保存已知序列化契约。
            /// </summary>
            private static readonly DefaultContractResolver Contracts = new DefaultContractResolver();
            /// <summary>
            /// 保存当前递归链。
            /// </summary>
            private readonly HashSet<object> _active = new HashSet<object>();
            /// <summary>
            /// 保存已访问对象。
            /// </summary>
            private readonly HashSet<object> _visited = new HashSet<object>();
            /// <summary>
            /// 保存主模型的局部标识索引。
            /// </summary>
            private Dictionary<string, Entry[]> _byId;
            /// <summary>
            /// 保存对象实例索引。
            /// </summary>
            private Dictionary<object, Entry> _byValue;
            /// <summary>
            /// 获取当前模型。
            /// </summary>
            public PdmInfo Model { get; }
            /// <summary>
            /// 获取对象定义。
            /// </summary>
            public List<Entry> Entries { get; } = new List<Entry>();
            /// <summary>
            /// 获取身份和集合问题。
            /// </summary>
            public List<PdmValidationIssue> Issues { get; } = new List<PdmValidationIssue>();

            /// <summary>
            /// 初始化一个 <see cref="Inventory"/> 类型的实例。
            /// </summary>
            public Inventory(PdmInfo model)
            {
                Model = model;
                Walk(model, null, "$", "Structure", model, null);
                _byId = Entries.Where(x => x.IdentityScope == model && x.Id != null)
                    .GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
                _byValue = Entries.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => x.First());
                foreach (var scope in Entries.GroupBy(x => x.IdentityScope))
                {
                    foreach (var group in scope.Where(x => !string.IsNullOrWhiteSpace(x.Id)).GroupBy(x => x.Id, StringComparer.Ordinal))
                    {
                        // 同一 XML User 同时投影为 Owner 和 Schema 是既有模型契约。
                        var projectedMetadata = group.Count() == 2 && group.Any(x => x.Value is PdmEmbeddedObjectInfo) &&
                            group.All(x => x.Scope == "Metadata") && group.Select(x => NormalizeGuid(x.Guid)).Distinct().Count() == 1;
                        if (group.Count() > 1 && !projectedMetadata && !(group.Count() == 2 &&
                            group.Any(x => x.Value is PdmOwnerInfo) && group.Any(x => x.Value is SchemaInfo)))
                            foreach (var entry in group) Add(Issues, entry, "DUPLICATE_ID", entry.Id, "identity");
                    }
                    foreach (var group in scope.Where(x => !string.IsNullOrWhiteSpace(x.Guid))
                        .GroupBy(x => x.Kind + ":" + NormalizeGuid(x.Guid), StringComparer.Ordinal).Where(x => x.Count() > 1))
                        foreach (var entry in group) Add(Issues, entry, "DUPLICATE_OBJECT_ID", entry.Guid, "identity");
                }
            }

            /// <summary>
            /// 递归收集定义并检测集合异常。
            /// </summary>
            private void Walk(object value, object parent, string path, string scope, object identityScope, PhysicalDiagramInfo diagram)
            {
                if (value == null) return;
                if (!(Contracts.ResolveContract(value.GetType()) is JsonObjectContract contract)) return;
                if (value is PhysicalDiagramInfo physical) { diagram = physical; scope = "Diagram"; }
                if (value is PdmReplicationInfo || value is PdmSubReplicationInfo || value is PdmEmbeddedObjectInfo ||
                    value.GetType().Name == "TargetModelInfo") scope = "Metadata";
                var entry = new Entry
                {
                    Value = value,
                    Parent = parent,
                    Path = path,
                    Kind = Kind(value),
                    Scope = scope,
                    Diagram = diagram,
                    IdentityScope = identityScope
                };
                if (_active.Contains(value)) { Add(Issues, entry, "COLLECTION_CYCLE", entry.Id, "collection"); return; }
                if (value is PdmCommonInfo || value is DiagramSymbolInfo)
                {
                    Entries.Add(entry);
                    if (string.IsNullOrWhiteSpace(entry.Id)) Add(Issues, entry, "MISSING_OBJECT_ID", null, "identity");
                }
                if (!_visited.Add(value)) return;
                _active.Add(value);
                foreach (var property in contract.Properties.Where(x => !x.Ignored && x.Readable))
                {
                    var child = property.ValueProvider.GetValue(value);
                    var childPath = path + "." + property.PropertyName;
                    if (child == null)
                    {
                        if (Contracts.ResolveContract(property.PropertyType) is JsonArrayContract)
                            Issues.Add(new PdmValidationIssue
                            {
                                Code = "NULL_MODEL_COLLECTION",
                                Kind = entry.Kind,
                                SourceId = entry.Id,
                                Path = childPath,
                                Scope = scope,
                                Role = property.PropertyName
                            });
                        continue;
                    }
                    if (child is string || child is IDictionary) continue;
                    if (child is IEnumerable collection)
                    {
                        var index = 0;
                        foreach (var item in collection)
                        {
                            var itemPath = childPath + "[" + index++ + "]";
                            if (item == null)
                            {
                                Issues.Add(new PdmValidationIssue
                                {
                                    Code = "NULL_MODEL_ITEM",
                                    Kind = entry.Kind,
                                    SourceId = entry.Id,
                                    Path = itemPath,
                                    Scope = scope,
                                    Role = property.PropertyName
                                });
                                continue;
                            }
                            var embedded = property.PropertyName.StartsWith("Embedded", StringComparison.Ordinal);
                            Walk(item, value, itemPath, scope, embedded ? value : identityScope, diagram);
                        }
                    }
                    else if (property.PropertyType.Namespace?.StartsWith("Bing.Pdm.Models", StringComparison.Ordinal) == true)
                        Walk(child, value, childPath, scope, identityScope, diagram);
                }
                _active.Remove(value);
            }

            /// <summary>
            /// 按类型与局部标识查找唯一业务对象。
            /// </summary>
            public Entry Find(string kind, string id)
            {
                if (id == null || !_byId.TryGetValue(id, out var entries)) return null;
                var matches = entries.Where(x => kind == null || x.Kind == kind).Take(2).ToArray();
                return matches.Length == 1 ? matches[0] : null;
            }
            /// <summary>
            /// 按实例查找定义。
            /// </summary>
            public Entry Find(object value) => value != null && _byValue.TryGetValue(value, out var entry) ? entry : null;
        }

        /// <summary>
        /// 模型及依赖引用的校验上下文。
        /// </summary>
        private sealed class ValidationContext
        {
            /// <summary>
            /// 保存当前模型清单。
            /// </summary>
            private readonly Inventory _inventory;
            /// <summary>
            /// 保存显式依赖工作区。
            /// </summary>
            private readonly PdmWorkspace _workspace;
            /// <summary>
            /// 保存当前模型键。
            /// </summary>
            private readonly string _modelKey;
            /// <summary>
            /// 保存结果集合。
            /// </summary>
            private readonly PdmValidationResult _result;
            /// <summary>
            /// 保存已读取的依赖清单。
            /// </summary>
            private readonly Dictionary<string, Inventory> _dependencies = new Dictionary<string, Inventory>(StringComparer.Ordinal);

            /// <summary>
            /// 初始化一个 <see cref="ValidationContext"/> 类型的实例。
            /// </summary>
            public ValidationContext(Inventory inventory, PdmWorkspace workspace, string modelKey, PdmValidationResult result)
            { _inventory = inventory; _workspace = workspace; _modelKey = modelKey; _result = result; }

            /// <summary>
            /// 校验业务关系和图元引用。
            /// </summary>
            public void Validate()
            {
                foreach (var entry in _inventory.Entries)
                {
                    if (entry.Value is TableInfo table)
                    {
                        Check(entry, "Package", table.PackageId, null, "package", false);
                        Check(entry, "Owner", table.OwnerId, null, "owner", false);
                        Check(entry, "Schema", table.SchemaId, null, "schema", false);
                        Check(entry, "Key", table.PrimaryKeyId, null, "primary key", false, table);
                    }
                    else if (entry.Value is Keys.KeyInfo key)
                    {
                        foreach (var id in (key.ColumnIds ?? new List<string>())) Check(entry, "Column", id, null, "key column", true, entry.Parent);
                        foreach (var address in (key.ColumnAddresses ?? new List<PdmObjectAddress>())) Check(entry, "Column", address?.PdmId, address, "key column", true, entry.Parent);
                    }
                    else if (entry.Value is IndexInfo index)
                        foreach (var id in (index.ColumnIds ?? new List<string>())) Check(entry, "Column", id, null, "index column", true, entry.Parent);
                    else if (entry.Value is IndexColumnInfo indexColumn)
                    {
                        var tableEntry = _inventory.Find(entry.Parent);
                        Check(entry, "Column", indexColumn.ColumnId, indexColumn.ColumnAddress, "index column", true, tableEntry?.Parent);
                    }
                    else if (entry.Value is PdmTriggerInfo trigger)
                    {
                        var owner = Check(entry, "Table", trigger.TableId, null, "trigger table", true);
                        if (owner != null && !ReferenceEquals(owner.Value, entry.Parent))
                            Add(_result.Issues, entry, "INVALID_REFERENCE_OWNER", trigger.TableId, "trigger table");
                    }
                    else if (entry.Value is ReferenceInfo reference) ValidateReference(entry, reference);
                    else if (entry.Value is PdmShortcutInfo shortcut)
                        Check(entry, shortcut.TargetKind ?? "Table", shortcut.ResolvedTargetId, shortcut.ResolvedTargetAddress, "shortcut target", true);
                    else if (entry.Value is Views.ViewInfo view) Check(entry, "Package", view.PackageId, null, "package", false);
                    else if (entry.Value is Views.ViewColumnInfo column)
                    {
                        var viewEntry = Check(entry, "View", column.ViewId, null, "view", false);
                        if (viewEntry != null && !ReferenceEquals(viewEntry.Value, entry.Parent))
                            Add(_result.Issues, entry, "INVALID_REFERENCE_OWNER", column.ViewId, "view");
                    }
                    else if (entry.Value is PhysicalDiagramInfo diagram) Check(entry, "Package", diagram.PackageId, null, "package", false);
                    else if (entry.Value is DiagramSymbolInfo symbol) ValidateSymbol(entry, symbol);
                    else if (entry.Value is TargetModelInfo target)
                    {
                        foreach (var id in (target.SessionShortcutRefs ?? new List<string>())) Check(entry, "Shortcut", id, null, "session shortcut", true);
                        foreach (var id in (target.SessionReplicationRefs ?? new List<string>())) MetadataRef(entry, id, "session replication", "PdmReplication", "PdmSubReplication");
                    }
                    else if (entry.Value is PdmReplicationInfo replication)
                    {
                        ValidateReplication(entry, replication.OriginalId, replication.OriginalAddress,
                            replication.ReplicaObjectRef, replication.ReplicaObjectAddress, replication.ReplicaObjectKind);
                        foreach (var id in (replication.SubReplicationRefs ?? new List<string>())) MetadataRef(entry, id, "sub replication", "PdmSubReplication");
                    }
                    else if (entry.Value is PdmSubReplicationInfo subReplication)
                    {
                        ValidateReplication(entry, subReplication.OriginalId, subReplication.OriginalAddress,
                            subReplication.ReplicaObjectRef, subReplication.ReplicaObjectAddress, subReplication.ReplicaObjectKind);
                        if (subReplication.ParentReplicationId != null)
                            MetadataRef(entry, subReplication.ParentReplicationId, "parent replication", "PdmReplication");
                    }
                    else if (entry.Value is PdmEmbeddedObjectInfo embedded && embedded.ParentId != null)
                        MetadataRef(entry, embedded.ParentId, "embedded parent", null);
                }
            }

            /// <summary>
            /// 校验独立元数据范围中的引用。
            /// </summary>
            private void MetadataRef(Entry source, string id, string role, params string[] kinds)
            {
                var matches = _inventory.Entries.Where(x => x.IdentityScope == source.IdentityScope && x.Id == id &&
                    (kinds == null || kinds.Contains(x.Kind))).ToArray();
                if (matches.Length != 1) Add(_result.Issues, source,
                    matches.Length == 0 ? "UNRESOLVED_MODEL_REF" : "AMBIGUOUS_MODEL_REF", id, role);
            }

            /// <summary>
            /// 校验复制来源和副本地址。
            /// </summary>
            private void ValidateReplication(Entry source, string originalId, PdmObjectAddress original,
                string replicaId, PdmObjectAddress replica, string replicaKind)
            {
                if (original != null) Check(source, null, original.PdmId, original, "replication origin", true);
                else
                {
                    var matches = _inventory.Entries.Where(x => !string.IsNullOrEmpty(originalId) &&
                        NormalizeGuid(x.Guid) == NormalizeGuid(originalId) &&
                        (x.IdentityScope == source.IdentityScope || x.IdentityScope == _inventory.Model)).ToArray();
                    if (matches.Length != 1) Add(_result.Issues, source,
                        matches.Length == 0 ? "UNRESOLVED_MODEL_REF" : "AMBIGUOUS_MODEL_REF", originalId, "replication origin");
                }
                if (source.IdentityScope == _inventory.Model || replica != null)
                    Check(source, replicaKind, replicaId, replica, "replica object", true);
                else
                {
                    var matches = _inventory.Entries.Where(x => x.IdentityScope == source.IdentityScope && x.Id == replicaId &&
                        x.Value is PdmEmbeddedObjectInfo item && (replicaKind == null || item.Kind == replicaKind)).ToArray();
                    if (matches.Length != 1) Add(_result.Issues, source, "UNRESOLVED_MODEL_REF", replicaId, "replica object");
                }
            }

            /// <summary>
            /// 校验外键端点及所属表。
            /// </summary>
            private void ValidateReference(Entry entry, ReferenceInfo reference)
            {
                var parent = Check(entry, "Table", reference.ParentTableId, reference.ParentTableAddress, "parent table", true);
                var child = Check(entry, "Table", reference.ChildTableId, reference.ChildTableAddress, "child table", true);
                var keyAddress = reference.ParentKeyAddress;
                if (keyAddress == null && reference.ParentTableAddress != null && _workspace != null)
                    keyAddress = new PdmObjectAddress { ModelKey = reference.ParentTableAddress.ModelKey, PdmId = reference.ParentKeyId };
                Check(entry, "Key", reference.ParentKeyId, keyAddress, "parent key", parent != null, parent?.Value);
                foreach (var join in (reference.Joins ?? new List<ReferenceJoinInfo>()).Where(x => x != null))
                {
                    var source = _inventory.Find(join);
                    var parentAddress = join.ParentColumnAddress;
                    var childAddress = join.ChildColumnAddress;
                    if (parentAddress == null && reference.ParentTableAddress != null && _workspace != null)
                        parentAddress = new PdmObjectAddress { ModelKey = reference.ParentTableAddress.ModelKey, PdmId = join.ParentColumnId };
                    if (childAddress == null && reference.ChildTableAddress != null && _workspace != null)
                        childAddress = new PdmObjectAddress { ModelKey = reference.ChildTableAddress.ModelKey, PdmId = join.ChildColumnId };
                    Check(source, "Column", join.ParentColumnId, parentAddress, "parent column", true, parent?.Value);
                    Check(source, "Column", join.ChildColumnId, childAddress, "child column", true, child?.Value);
                }
            }

            /// <summary>
            /// 校验物理图对象及连线端点。
            /// </summary>
            private void ValidateSymbol(Entry entry, DiagramSymbolInfo symbol)
            {
                if (symbol.Kind == "TableSymbol" || symbol.Kind == "PackageSymbol" || symbol.Kind == "ReferenceSymbol")
                    Check(entry, symbol.Kind.Replace("Symbol", ""), symbol.ObjectId, symbol.ObjectAddress, "diagram object", true);
                foreach (var id in new[] { symbol.SourceSymbolId, symbol.DestinationSymbolId }.Where(x => x != null))
                    if (!_inventory.Entries.Any(x => x.Diagram == entry.Diagram && x.Value is DiagramSymbolInfo && x.Id == id))
                        Add(_result.Issues, entry, "UNRESOLVED_MODEL_REF", id, "diagram endpoint");
            }

            /// <summary>
            /// 校验引用类型、地址与所属对象。
            /// </summary>
            private Entry Check(Entry source, string kind, string id, PdmObjectAddress address, string role, bool required, object parent = null)
            {
                if (address == null && string.IsNullOrEmpty(id) && !required) return null;
                var inventory = _inventory;
                if (address != null)
                {
                    if (string.IsNullOrWhiteSpace(address.ModelKey) || string.IsNullOrWhiteSpace(address.PdmId))
                    { Add(_result.Issues, source, "UNRESOLVED_MODEL_REF", address.ToString(), role); return null; }
                    if (_workspace != null)
                    {
                        if (!_workspace.TryGetModel(address.ModelKey, out var target))
                        { Add(_result.Issues, source, "UNRESOLVED_MODEL_REF", address.ToString(), role); return null; }
                        if (address.ModelKey != _modelKey)
                        {
                            if (!_dependencies.TryGetValue(address.ModelKey, out inventory))
                                _dependencies[address.ModelKey] = inventory = new Inventory(target);
                        }
                    }
                    else
                    {
                        var local = inventory.Find(kind, address.PdmId);
                        var localAddress = _modelKey != null ? address.ModelKey == _modelKey :
                            local != null && !string.IsNullOrEmpty(address.ObjectId) && NormalizeGuid(local.Guid) == NormalizeGuid(address.ObjectId);
                        if (!localAddress)
                        { Add(_result.Issues, source, "EXTERNAL_TARGET_UNVERIFIED", address.ToString(), role); return null; }
                    }
                    id = address.PdmId;
                }
                var match = inventory.Find(kind, id);
                if (match == null || (address?.ObjectId != null && NormalizeGuid(address.ObjectId) != NormalizeGuid(match.Guid)))
                { Add(_result.Issues, source, "UNRESOLVED_MODEL_REF", address?.ToString() ?? id, role); return null; }
                if (parent != null && !ReferenceEquals(match.Parent, parent))
                    Add(_result.Issues, source, "INVALID_REFERENCE_OWNER", address?.ToString() ?? id, role);
                return match;
            }
        }

        /// <summary>
        /// 获取统一模型类型名称。
        /// </summary>
        private static string Kind(object value)
        {
            var name = value.GetType().Name;
            if (value is PdmInfo) return "Model";
            if (value is PdmOwnerInfo) return "Owner";
            if (value is PdmShortcutInfo) return "Shortcut";
            return name.EndsWith("Info", StringComparison.Ordinal) ? name.Substring(0, name.Length - 4) : name;
        }

        /// <summary>
        /// 统一 GUID 文本表示。
        /// </summary>
        private static string NormalizeGuid(string value) => PdmIdentity.Normalize(value);

        /// <summary>
        /// 记录对象问题。
        /// </summary>
        private static void Add(List<PdmValidationIssue> issues, Entry source, string code, string target, string role) =>
            issues.Add(new PdmValidationIssue
            {
                Code = code,
                Kind = source.Kind,
                SourceId = source.Id,
                Path = source.Path,
                TargetId = target,
                Role = role,
                Scope = source.Scope
            });
    }
}
