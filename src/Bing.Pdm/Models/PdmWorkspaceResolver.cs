using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Pdm.Models.Keys;
using Bing.Pdm.Models.PhysicalDiagrams;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Models.Views;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 在显式模型工作区中解析限定模型的对象引用。
    /// </summary>
    public sealed class PdmWorkspaceResolver
    {
        /// <summary>
        /// 获取候选对象所属模型与类型。
        /// </summary>
        private sealed class Candidate
        {
            /// <summary>
            /// 获取候选对象所属的工作区模型键。
            /// </summary>
            public string Key;
            /// <summary>
            /// 获取候选对象的模型类型。
            /// </summary>
            public string Kind;
            /// <summary>
            /// 获取候选对象实例。
            /// </summary>
            public PdmCommonInfo Value;
            /// <summary>
            /// 获取候选对象的直接父对象标识。
            /// </summary>
            public string ParentId;
            /// <summary>
            /// 获取候选对象在工作区中的地址。
            /// </summary>
            public PdmObjectAddress Address => new PdmObjectAddress
                { ModelKey = Key, PdmId = Value.Id, ObjectId = Value.ObjectId };
        }

        /// <summary>
        /// 解析全部模型的 Shortcut、关系和复制来源。
        /// </summary>
        /// <param name="workspace">待解析的显式模型工作区。</param>
        public void Resolve(PdmWorkspace workspace)
        {
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            workspace.ResetDiagnostics();
            var objects = workspace.Models.SelectMany(x => Candidates(x.Key, x.Value)).ToArray();
            foreach (var shortcut in workspace.Models.Values.SelectMany(x => x.AllShortcuts))
            {
                shortcut.ResolvedTargetAddress = null;
                shortcut.ResolvedTargetId = null;
            }
            foreach (var pair in workspace.Models)
            {
                var key = pair.Key;
                var model = pair.Value;
                foreach (var shortcut in model.AllShortcuts)
                    ResolveShortcut(workspace, objects, key, model, shortcut,
                        new HashSet<string>(StringComparer.Ordinal));
                foreach (var reference in model.AllReferences)
                    ResolveReference(workspace, objects, key, model, reference);
                foreach (var symbol in model.AllPhysicalDiagrams.SelectMany(x => x.AllSymbols))
                {
                    symbol.ObjectAddress = null;
                    if (symbol.Kind != "TableSymbol" && symbol.Kind != "PackageSymbol" &&
                        symbol.Kind != "ReferenceSymbol") continue;
                    var kind = symbol.Kind.Substring(0, symbol.Kind.Length - "Symbol".Length);
                    symbol.ObjectAddress = ResolveRef(workspace, objects, key, model,
                        symbol.RawObjectRef ?? symbol.ObjectId, kind, null, null, symbol.Id, "diagram object");
                }
                foreach (var table in model.AllTables)
                {
                    foreach (var primary in table.Keys)
                    {
                        primary.ColumnAddresses.Clear();
                        foreach (var id in primary.ColumnIds)
                        {
                            var address = ResolveRef(workspace, objects, key, model, id, "Column", key,
                                table.Id, primary.Id, "key column");
                            if (address != null) primary.ColumnAddresses.Add(address);
                        }
                    }
                    foreach (var index in table.Indexes)
                        foreach (var indexColumn in index.IndexColumns)
                            indexColumn.ColumnAddress = ResolveRef(workspace, objects, key, model,
                                indexColumn.ColumnId, "Column", key, table.Id, indexColumn.Id, "index column");
                }
                foreach (var replication in model.AllReplications)
                {
                    replication.OriginalAddress = ResolveReplicationOrigin(workspace, objects, key, model,
                        replication.OriginalId, replication.Id, replication.Id, "replication origin");
                    replication.ReplicaObjectAddress = ResolveRef(workspace, objects, key, model,
                        replication.ReplicaObjectRef, replication.ReplicaObjectKind, key, null,
                        replication.Id, "replica object");
                }
                foreach (var child in model.AllSubReplications)
                {
                    child.OriginalAddress = ResolveReplicationOrigin(workspace, objects, key, model,
                        child.OriginalId, child.ParentReplicationId ?? child.Id, child.Id,
                        "sub-replication origin");
                    child.ReplicaObjectAddress = ResolveRef(workspace, objects, key, model,
                        child.ReplicaObjectRef, child.ReplicaObjectKind, key, null,
                        child.Id, "replica object");
                }
            }
        }

        /// <summary>
        /// 根据会话模型与对象 GUID 解析复制来源。
        /// </summary>
        private static PdmObjectAddress ResolveReplicationOrigin(PdmWorkspace workspace,
            Candidate[] objects, string key, PdmInfo model, string guid,
            string sessionRef, string sourceId, string role)
        {
            var targets = model.AllTargetModels.Where(x =>
                x.SessionReplicationRefs.Contains(sessionRef)).ToArray();
            if (targets.Length > 1)
            {
                workspace.AddDiagnostic(key, "AMBIGUOUS_WORKSPACE_MODEL", sourceId,
                    "Replication belongs to multiple target model sessions.");
                return null;
            }
            string targetKey = null;
            if (targets.Length == 1)
            {
                targetKey = workspace.Models.FirstOrDefault(x =>
                    PdmIdentity.Equals(x.Value.ObjectId, targets[0].TargetModelId)).Key;
                if (targetKey == null)
                {
                    workspace.AddDiagnostic(key, "UNRESOLVED_WORKSPACE_MODEL", sourceId,
                        "Replication target model is not in the workspace manifest.");
                    return null;
                }
            }
            return ResolveGuid(workspace, objects, key, guid, sourceId, role, targetKey);
        }

        /// <summary>
        /// 按会话限定与 GUID 解析 Shortcut 链。
        /// </summary>
        private static PdmObjectAddress ResolveShortcut(PdmWorkspace workspace, Candidate[] objects,
            string key, PdmInfo model, PdmShortcutInfo shortcut, HashSet<string> visited)
        {
            if (shortcut.ResolvedTargetAddress != null) return shortcut.ResolvedTargetAddress;
            var visitKey = key + ":" + shortcut.Id;
            if (!visited.Add(visitKey))
            {
                workspace.AddDiagnostic(key, "CYCLIC_SHORTCUT", shortcut.Id,
                    "Shortcut target chain contains a cycle.");
                return null;
            }
            var sessions = model.AllTargetModels
                .Where(x => x.SessionShortcutRefs.Contains(shortcut.Id)).ToArray();
            var sessionKeys = sessions
                .Select(x => workspace.Models.FirstOrDefault(y =>
                    PdmIdentity.Equals(y.Value.ObjectId, x.TargetModelId)).Key)
                .Where(x => x != null).Distinct(StringComparer.Ordinal).ToArray();
            if (sessions.Length > 1 || sessionKeys.Length > 1)
            {
                workspace.AddDiagnostic(key, "AMBIGUOUS_WORKSPACE_MODEL", shortcut.Id,
                    "Shortcut belongs to multiple target model sessions.");
                visited.Remove(visitKey);
                return null;
            }
            if (sessions.Length > 0 && sessionKeys.Length == 0)
            {
                workspace.AddDiagnostic(key, "UNRESOLVED_WORKSPACE_MODEL", shortcut.Id,
                    "The Shortcut session target model is not in the workspace manifest.");
                visited.Remove(visitKey);
                return null;
            }
            var matches = objects.Where(x => x.Kind == shortcut.TargetKind &&
                !(x.Value is PdmEmbeddedObjectInfo) &&
                PdmIdentity.Equals(x.Value.ObjectId, shortcut.TargetId) &&
                (sessionKeys.Length == 0 || sessionKeys.Contains(x.Key))).ToArray();
            if (matches.Length == 1)
                shortcut.ResolvedTargetAddress = matches[0].Address;
            else if (matches.Length == 0)
            {
                var chained = workspace.Models.SelectMany(x => x.Value.AllShortcuts
                        .Where(s => PdmIdentity.Equals(s.ObjectId, shortcut.TargetId) &&
                            s.TargetKind == shortcut.TargetKind &&
                            (sessionKeys.Length == 0 || sessionKeys.Contains(x.Key)))
                        .Select(s => Tuple.Create(x.Key, x.Value, s))).ToArray();
                if (chained.Length == 1)
                    shortcut.ResolvedTargetAddress = ResolveShortcut(workspace, objects, chained[0].Item1,
                        chained[0].Item2, chained[0].Item3, visited);
                else if (chained.Length > 1)
                    matches = new Candidate[chained.Length];
            }
            if (shortcut.ResolvedTargetAddress == null && !workspace.GetOwnDiagnostics(key)
                .Any(x => x.Code == "CYCLIC_SHORTCUT" && x.SourceId == shortcut.Id))
                workspace.AddDiagnostic(key, matches.Length > 1 ? "AMBIGUOUS_WORKSPACE_TARGET" : "UNRESOLVED_WORKSPACE_TARGET",
                    shortcut.Id, "Shortcut target '" + shortcut.TargetId + "' has " + matches.Length +
                    " matching " + shortcut.TargetKind + " objects.");
            shortcut.ResolvedTargetId = shortcut.ResolvedTargetAddress?.ModelKey == key
                ? shortcut.ResolvedTargetAddress.PdmId : null;
            visited.Remove(visitKey);
            return shortcut.ResolvedTargetAddress;
        }

        /// <summary>
        /// 解析外键各端点及列关联。
        /// </summary>
        private static void ResolveReference(PdmWorkspace workspace, Candidate[] objects, string key,
            PdmInfo model, ReferenceInfo reference)
        {
            reference.ParentTableAddress = ResolveRef(workspace, objects, key, model,
                reference.RawParentTableRef ?? reference.ParentTableId, "Table", null, null,
                reference.Id, "parent table");
            reference.ChildTableAddress = ResolveRef(workspace, objects, key, model,
                reference.RawChildTableRef ?? reference.ChildTableId, "Table", null, null,
                reference.Id, "child table");
            reference.ParentKeyAddress = ResolveRef(workspace, objects, key, model,
                reference.ParentKeyId, "Key", reference.ParentTableAddress?.ModelKey,
                reference.ParentTableAddress?.PdmId, reference.Id, "parent key");
            foreach (var join in reference.Joins)
            {
                join.ParentColumnAddress = ResolveRef(workspace, objects, key, model,
                    join.ParentColumnId, "Column", reference.ParentTableAddress?.ModelKey,
                    reference.ParentTableAddress?.PdmId, join.Id, "parent column");
                join.ChildColumnAddress = ResolveRef(workspace, objects, key, model,
                    join.ChildColumnId, "Column", reference.ChildTableAddress?.ModelKey,
                    reference.ChildTableAddress?.PdmId, join.Id, "child column");
            }
        }

        /// <summary>
        /// 按工作区内的唯一 GUID 定位来源对象。
        /// </summary>
        private static PdmObjectAddress ResolveGuid(PdmWorkspace workspace, Candidate[] objects, string key,
            string guid, string sourceId, string role, string preferredKey)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            var matches = objects.Where(x => !(x.Value is PdmEmbeddedObjectInfo) &&
                    PdmIdentity.Equals(x.Value.ObjectId, guid) &&
                    (preferredKey == null || x.Key == preferredKey))
                .ToArray();
            if (matches.Length == 1) return matches[0].Address;
            workspace.AddDiagnostic(key, matches.Length > 1 ? "AMBIGUOUS_WORKSPACE_TARGET" : "UNRESOLVED_WORKSPACE_TARGET",
                sourceId, role + " GUID '" + guid + "' has " + matches.Length + " matching objects.");
            return null;
        }

        /// <summary>
        /// 解析本地 ID、Shortcut 或被目标模型限定的外部 ID。
        /// </summary>
        private static PdmObjectAddress ResolveRef(PdmWorkspace workspace, Candidate[] objects, string key,
            PdmInfo model, string rawId, string kind, string preferredKey, string parentId,
            string sourceId, string role)
        {
            if (string.IsNullOrEmpty(rawId)) return null;
            if (model.Lookup.TryGetShortcut(rawId, out var shortcut))
            {
                if (kind == "Shortcut")
                    return new PdmObjectAddress { ModelKey = key, PdmId = shortcut.Id,
                        ObjectId = shortcut.ObjectId };
                var address = shortcut.ResolvedTargetAddress;
                var target = address == null ? null : objects.FirstOrDefault(x =>
                    x.Key == address.ModelKey && x.Value.Id == address.PdmId);
                if (target != null && (kind == null || target.Kind == kind) &&
                    (preferredKey == null || address.ModelKey == preferredKey) &&
                    (parentId == null || target.ParentId == parentId))
                    return address;
                if (address != null)
                    workspace.AddDiagnostic(key, "INVALID_WORKSPACE_REFERENCE", sourceId,
                        role + " Shortcut '" + rawId + "' does not match its required type or parent.");
                return null;
            }
            var candidates = objects.Where(x => x.Value.Id == rawId &&
                (kind == null || x.Kind == kind) &&
                (parentId == null || x.ParentId == parentId)).ToArray();
            candidates = candidates.Where(x => x.Key == (preferredKey ?? key)).ToArray();
            if (candidates.Length == 1) return candidates[0].Address;
            workspace.AddDiagnostic(key, candidates.Length > 1 ? "AMBIGUOUS_WORKSPACE_TARGET" : "UNRESOLVED_WORKSPACE_TARGET",
                sourceId, role + " reference '" + rawId + "' has " + candidates.Length + " matching objects.");
            return null;
        }

        /// <summary>
        /// 枚举一个主模型及其嵌入对象的可寻址候选。
        /// </summary>
        private static IEnumerable<Candidate> Candidates(string key, PdmInfo model)
        {
            yield return Item(key, "Model", model);
            foreach (var package in AllPackages(model.Packages))
                yield return Item(key, "Package", package);
            foreach (var table in model.AllTables)
            {
                yield return Item(key, "Table", table);
                foreach (var column in table.Columns) yield return Item(key, "Column", column, table.Id);
                foreach (var primary in table.Keys) yield return Item(key, "Key", primary, table.Id);
                foreach (var trigger in table.Triggers) yield return Item(key, "Trigger", trigger, table.Id);
            }
            foreach (var view in model.AllViews)
            {
                yield return Item(key, "View", view);
                foreach (var column in view.Columns) yield return Item(key, "ViewColumn", column, view.Id);
            }
            foreach (var reference in model.AllReferences)
                yield return Item(key, "Reference", reference);
            foreach (var target in model.AllTargetModels)
                foreach (var embedded in target.EmbeddedObjects)
                    yield return Item(key, embedded.Kind, embedded, embedded.ParentId);
        }

        /// <summary>
        /// 枚举嵌套包。
        /// </summary>
        private static IEnumerable<PackageInfo> AllPackages(IEnumerable<PackageInfo> packages)
        {
            foreach (var package in packages)
            {
                yield return package;
                foreach (var child in AllPackages(package.Packages)) yield return child;
            }
        }

        /// <summary>
        /// 创建候选对象。
        /// </summary>
        private static Candidate Item(string key, string kind, PdmCommonInfo value, string parentId = null) =>
            new Candidate { Key = key, Kind = kind, Value = value, ParentId = parentId };
    }
}
