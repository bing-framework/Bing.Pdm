using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Bing.Pdm.Models;
using Bing.Pdm.Models.Others;

namespace Bing.Pdm.Reader
{
    /// <summary>
    /// 读取 PowerDesigner 的目标模型和复制元数据。
    /// </summary>
    internal sealed partial class PowerDesignerParser
    {
        /// <summary>
        /// 读取当前模型或包直接拥有的元数据。
        /// </summary>
        private static void ReadMetadata(XmlElement node, List<TargetModelInfo> targets,
            List<PdmReplicationInfo> replications, List<PdmSubReplicationInfo> subReplications)
        {
            foreach (var element in Objects(node, "TargetModels", "TargetModel"))
            {
                var target = new TargetModelInfo
                {
                    TargetModelId = Value(element, "TargetModelID"),
                    TargetUrl = Value(element, "TargetModelURL"),
                    TargetClassId = Value(element, "TargetModelClassID"),
                    TargetLastModificationDate = Date(Value(element, "TargetModelLastModificationDate"))
                };
                target.TargetId = target.TargetModelId;
                ReadCommon(element, target);
                ReadUnknownAttributes(element, target.RawAttributes, "TargetModelID", "TargetModelURL",
                    "TargetModelClassID", "TargetModelLastModificationDate");
                target.SessionShortcutRefs.AddRange(Refs(element, "SessionShortcuts"));
                target.SessionReplicationRefs.AddRange(Refs(element, "SessionReplications"));
                foreach (var full in Elements(Child(element, CollectionNs, "FullShortcutModel"))
                    .Where(x => x.NamespaceURI == ObjectNs && x.HasAttribute("Id")))
                {
                    ReadEmbedded(full, null, target.EmbeddedObjects);
                    ReadEmbeddedReplications(full, target.EmbeddedReplications, target.EmbeddedSubReplications);
                }
                targets.Add(target);
            }
            foreach (var element in Objects(node, "Replications", "Replication"))
                replications.Add(ReadReplication(element));
            foreach (var element in Objects(node, "SubReplications", "SubReplication"))
                subReplications.Add(ReadSubReplication(element, null));
        }

        /// <summary>
        /// 读取嵌入模型各层包中的复制关系。
        /// </summary>
        private static void ReadEmbeddedReplications(XmlElement node, List<PdmReplicationInfo> replications,
            List<PdmSubReplicationInfo> subReplications)
        {
            foreach (var item in Objects(node, "Replications", "Replication"))
                replications.Add(ReadReplication(item));
            foreach (var item in Objects(node, "SubReplications", "SubReplication"))
                subReplications.Add(ReadSubReplication(item, null));
            foreach (var child in Objects(node, "Packages", "Package"))
                ReadEmbeddedReplications(child, replications, subReplications);
        }

        /// <summary>
        /// 读取复制关系及其子对象映射。
        /// </summary>
        private static PdmReplicationInfo ReadReplication(XmlElement element)
        {
            var replica = Replica(element);
            var result = new PdmReplicationInfo
            {
                OriginalId = Value(element, "OriginalID"),
                OriginalClassId = Value(element, "OriginalClassID"),
                ReplicaObjectRef = ReplicaId(replica),
                ReplicaObjectKind = replica?.LocalName
            };
            ReadCommon(element, result);
            ReadUnknownAttributes(element, result.RawAttributes, "OriginalID", "OriginalClassID");
            result.SubReplicationRefs.AddRange(Refs(element, "SubReplications"));
            foreach (var child in Objects(element, "SubReplications", "SubReplication"))
                result.SubReplications.Add(ReadSubReplication(child, result.Id));
            return result;
        }

        /// <summary>
        /// 读取一个子对象复制关系。
        /// </summary>
        private static PdmSubReplicationInfo ReadSubReplication(XmlElement element, string parentId)
        {
            var replica = Replica(element);
            var result = new PdmSubReplicationInfo
            {
                ParentReplicationId = parentId,
                OriginalId = Value(element, "OriginalID"),
                OriginalClassId = Value(element, "OriginalClassID"),
                ReplicaObjectRef = ReplicaId(replica),
                ReplicaObjectKind = replica?.LocalName
            };
            ReadCommon(element, result);
            ReadUnknownAttributes(element, result.RawAttributes, "OriginalID", "OriginalClassID");
            return result;
        }

        /// <summary>
        /// 读取副本对象引用节点。
        /// </summary>
        private static XmlElement Replica(XmlElement node) =>
            Elements(Child(node, CollectionNs, "ReplicaObject"))
                .FirstOrDefault(x => x.NamespaceURI == ObjectNs);

        /// <summary>
        /// 取得副本定义或引用的标识。
        /// </summary>
        private static string ReplicaId(XmlElement replica) => replica == null ? null :
            replica.HasAttribute("Ref") ? replica.GetAttribute("Ref") :
            replica.HasAttribute("Id") ? replica.GetAttribute("Id") : null;

        /// <summary>
        /// 将子复制引用关联到已有定义。
        /// </summary>
        private static void BindSubReplicationRefs(PdmInfo model)
        {
            var children = model.AllSubReplications
                .Where(x => !string.IsNullOrEmpty(x.Id))
                .GroupBy(x => x.Id, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
            foreach (var replication in model.AllReplications)
                foreach (var id in replication.SubReplicationRefs)
                {
                    if (!children.TryGetValue(id, out var matches) || matches.Length != 1)
                    {
                        model.Diagnostics.Add(Diagnostic(matches == null ? "UNRESOLVED_SUB_REPLICATION" :
                            "AMBIGUOUS_SUB_REPLICATION", replication.Id,
                            "Sub-replication reference '" + id + "' has " +
                            (matches?.Length ?? 0) + " matching definitions."));
                        continue;
                    }
                    if (matches[0].ParentReplicationId != null &&
                        matches[0].ParentReplicationId != replication.Id)
                    {
                        model.Diagnostics.Add(Diagnostic("AMBIGUOUS_SUB_REPLICATION", replication.Id,
                            "Sub-replication '" + id + "' belongs to another replication."));
                        continue;
                    }
                    matches[0].ParentReplicationId = replication.Id;
                }
        }

        /// <summary>
        /// 将嵌入模型的定义对象与主业务对象分开保存。
        /// </summary>
        private static void ReadEmbedded(XmlElement node, string parentId, List<PdmEmbeddedObjectInfo> items)
        {
            var nextParentId = parentId;
            if (node.NamespaceURI == ObjectNs && node.HasAttribute("Id"))
            {
                var item = new PdmEmbeddedObjectInfo
                {
                    Kind = node.LocalName,
                    ParentId = parentId,
                    TargetId = Value(node, "TargetID")
                };
                ReadCommon(node, item);
                ReadUnknownAttributes(node, item.RawAttributes, "TargetID");
                items.Add(item);
                nextParentId = item.Id;
            }
            foreach (var child in Elements(node))
                ReadEmbedded(child, nextParentId, items);
        }

        /// <summary>
        /// 保留当前对象中尚未映射的简单 XML 属性。
        /// </summary>
        private static void ReadUnknownAttributes(XmlElement node, Dictionary<string, string> values, params string[] known)
        {
            var common = new HashSet<string>(known, StringComparer.Ordinal)
            {
                "ObjectID", "Name", "Code", "Comment", "Creator", "Modifier",
                "CreationDate", "ModificationDate"
            };
            foreach (var element in Elements(node)
                .Where(x => x.NamespaceURI == AttributeNs && !common.Contains(x.LocalName)))
                values[element.LocalName] = element.InnerText;
        }

        /// <summary>
        /// 验证会话引用和复制来源是否可以定位。
        /// </summary>
        private static void ValidateMetadata(PdmInfo model, XmlElement source)
        {
            var targets = model.AllTargetModels.ToArray();
            var replications = model.AllReplications.ToArray();
            var children = model.AllSubReplications.ToArray();
            var embedded = targets.SelectMany(x => x.EmbeddedObjects).ToArray();
            var known = new HashSet<string>(CommonObjects(model).Select(x => x.Id)
                .Concat(embedded.Select(x => x.Id))
                .Concat(replications.Select(x => x.Id))
                .Concat(children.Select(x => x.Id)), StringComparer.Ordinal);
            foreach (var dbms in Objects(source, "DBMS", "Shortcut"))
                known.Add(dbms.GetAttribute("Id"));
            var knownGuids = new HashSet<string>(CommonObjects(model).Select(x => PdmIdentity.Normalize(x.ObjectId))
                .Concat(embedded.Select(x => PdmIdentity.Normalize(x.ObjectId))), StringComparer.Ordinal);
            foreach (var target in targets)
            {
                foreach (var id in target.SessionShortcutRefs.Concat(target.SessionReplicationRefs)
                    .Where(x => !string.IsNullOrEmpty(x) && !known.Contains(x)))
                    model.Diagnostics.Add(Diagnostic("UNRESOLVED_SESSION_REF", target.Id,
                        "Session reference '" + id + "' cannot be resolved."));
            }
            foreach (var item in replications.Select(x => (x.Id, x.OriginalId, x.ReplicaObjectRef))
                .Concat(children.Select(x => (x.Id, x.OriginalId, x.ReplicaObjectRef))))
            {
                if (!string.IsNullOrEmpty(item.ReplicaObjectRef) && !known.Contains(item.ReplicaObjectRef))
                    model.Diagnostics.Add(Diagnostic("UNRESOLVED_REPLICA_OBJECT", item.Id,
                        "Replica object '" + item.ReplicaObjectRef + "' cannot be resolved."));
                if (!string.IsNullOrEmpty(item.OriginalId) && !knownGuids.Contains(PdmIdentity.Normalize(item.OriginalId)))
                    model.Diagnostics.Add(Diagnostic("UNRESOLVED_REPLICATION_ORIGIN", item.Id,
                        "Replication origin '" + item.OriginalId + "' is outside the root model."));
            }
            foreach (var replica in replications.Select(x => x.ReplicaObjectRef)
                .Concat(children.Select(x => x.ReplicaObjectRef))
                .Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.Ordinal))
                if (model.MetadataLookup.TraceOrigins(replica).CycleDetected)
                    model.Diagnostics.Add(Diagnostic("CYCLIC_REPLICATION", replica,
                        "Replication lineage contains a cycle."));
        }
    }
}
