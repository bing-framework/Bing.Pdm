using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Pdm.Models.Keys;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Models.Views;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 结构比较选项。
    /// </summary>
    public sealed class PdmCompareOptions
    {
        /// <summary>
        /// 获取或设置名称比较是否忽略大小写。
        /// </summary>
        public bool IgnoreCase { get; set; }
        /// <summary>
        /// 获取或设置旧模型依赖工作区。
        /// </summary>
        public PdmWorkspace BeforeWorkspace { get; set; }
        /// <summary>
        /// 获取或设置新模型依赖工作区。
        /// </summary>
        public PdmWorkspace AfterWorkspace { get; set; }
        /// <summary>
        /// 获取或设置旧模型在工作区中的键。
        /// </summary>
        public string BeforeModelKey { get; set; }
        /// <summary>
        /// 获取或设置新模型在工作区中的键。
        /// </summary>
        public string AfterModelKey { get; set; }
    }

    /// <summary>
    /// 一项数据库结构差异。
    /// </summary>
    public sealed class PdmDiffChange
    {
        /// <summary>
        /// 获取或设置对象种类。
        /// </summary>
        public string Kind { get; set; }
        /// <summary>
        /// 获取或设置变更类型。
        /// </summary>
        public string ChangeType { get; set; }
        /// <summary>
        /// 获取或设置旧对象标识。
        /// </summary>
        public string BeforeId { get; set; }
        /// <summary>
        /// 获取或设置新对象标识。
        /// </summary>
        public string AfterId { get; set; }
        /// <summary>
        /// 获取或设置旧对象路径。
        /// </summary>
        public string BeforePath { get; set; }
        /// <summary>
        /// 获取或设置新对象路径。
        /// </summary>
        public string AfterPath { get; set; }
        /// <summary>
        /// 获取或设置变化的属性。
        /// </summary>
        public string Property { get; set; }
        /// <summary>
        /// 获取或设置旧属性值。
        /// </summary>
        public string BeforeValue { get; set; }
        /// <summary>
        /// 获取或设置新属性值。
        /// </summary>
        public string AfterValue { get; set; }
    }

    /// <summary>
    /// 差异比较一侧的结构校验问题。
    /// </summary>
    public sealed class PdmDiffValidationIssue
    {
        /// <summary>
        /// 获取或设置问题所在侧。
        /// </summary>
        /// <remarks>取值为 Before 或 After。</remarks>
        public string Side { get; set; }
        /// <summary>
        /// 获取或设置校验问题。
        /// </summary>
        public PdmValidationIssue Issue { get; set; }
    }

    /// <summary>
    /// 数据库结构差异报告。
    /// </summary>
    public sealed class PdmDiffResult
    {
        /// <summary>
        /// 获取或设置旧模型 DBMS。
        /// </summary>
        public string BeforeDbms { get; set; }
        /// <summary>
        /// 获取或设置新模型 DBMS。
        /// </summary>
        public string AfterDbms { get; set; }
        /// <summary>
        /// 获取固定排序的差异项。
        /// </summary>
        public List<PdmDiffChange> Changes { get; } = new List<PdmDiffChange>();
        /// <summary>
        /// 获取两侧的结构校验问题。
        /// </summary>
        public List<PdmDiffValidationIssue> ValidationIssues { get; } = new List<PdmDiffValidationIssue>();
        /// <summary>
        /// 获取结构比较是否不完整。
        /// </summary>
        public bool Incomplete => ValidationIssues.Count > 0 || Changes.Any(x => x.ChangeType == "Ambiguous");
    }

    /// <summary>
    /// 比较两个模型的数据库结构。
    /// </summary>
    public sealed class PdmModelComparer
    {
        /// <summary>
        /// 比较表、列、键、索引、引用和视图。
        /// </summary>
        /// <param name="before">比较前的 PDM 模型。</param>
        /// <param name="after">比较后的 PDM 模型。</param>
        /// <param name="options">可选的名称比较和工作区解析选项。</param>
        /// <returns>包含结构差异和校验问题的报告。</returns>
        public PdmDiffResult Compare(PdmInfo before, PdmInfo after, PdmCompareOptions options = null)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));
            var result = new PdmDiffResult { BeforeDbms = before.DbmsCode ?? before.DbmsName,
                AfterDbms = after.DbmsCode ?? after.DbmsName };
            var validator = new PdmModelValidator();
            result.ValidationIssues.AddRange(validator.Validate(before, options?.BeforeWorkspace, options?.BeforeModelKey)
                .Issues.Where(x => x.Scope == "Structure").Select(x => new PdmDiffValidationIssue { Side = "Before", Issue = x }));
            result.ValidationIssues.AddRange(validator.Validate(after, options?.AfterWorkspace, options?.AfterModelKey)
                .Issues.Where(x => x.Scope == "Structure").Select(x => new PdmDiffValidationIssue { Side = "After", Issue = x }));
            // 无法安全遍历的集合保留问题报告，避免递归溢出或生成推测差异。
            if (result.ValidationIssues.Any(x => x.Issue.Code == "COLLECTION_CYCLE" ||
                x.Issue.Code == "NULL_MODEL_ITEM" || x.Issue.Code == "NULL_MODEL_COLLECTION")) return result;
            var comparison = options?.IgnoreCase == true ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var tables = Match(before.AllTables.ToArray(), after.AllTables.ToArray(), "Table",
                x => TablePath(before, x), x => TablePath(after, x), comparison, result);
            var identities = new ComparisonIdentities(options);
            identities.Add(tables);
            foreach (var pair in tables)
            {
                CompareCommon(pair.Item1, pair.Item2, "Table", TablePath(before, pair.Item1),
                    TablePath(after, pair.Item2), result, comparison);
                Field(result, "Table", pair.Item1, pair.Item2, "Description",
                    pair.Item1.Description, pair.Item2.Description,
                    TablePath(before, pair.Item1), TablePath(after, pair.Item2));
                var columns = Match(pair.Item1.Columns.ToArray(), pair.Item2.Columns.ToArray(), "Column",
                    x => x.Code, x => x.Code, comparison, result,
                    x => TablePath(before, pair.Item1) + "." + x.Code,
                    x => TablePath(after, pair.Item2) + "." + x.Code);
                identities.Add(columns);
                IdentityField(result, "Table", pair.Item1, pair.Item2, "ColumnOrder",
                    pair.Item1.Columns.Select(x => identities.Get(x, false)),
                    pair.Item2.Columns.Select(x => identities.Get(x, true)),
                    string.Join(",", pair.Item1.Columns.Select(ColumnIdentity)),
                    string.Join(",", pair.Item2.Columns.Select(ColumnIdentity)),
                    TablePath(before, pair.Item1), TablePath(after, pair.Item2));
                foreach (var column in columns)
                {
                    var a = column.Item1; var b = column.Item2;
                    var oldColumnPath = TablePath(before, pair.Item1) + "." + a.Code;
                    var newColumnPath = TablePath(after, pair.Item2) + "." + b.Code;
                    CompareCommon(a, b, "Column", oldColumnPath, newColumnPath, result, comparison);
                    Field(result, "Column", a, b, "DataType", a.DataType, b.DataType, oldColumnPath, newColumnPath);
                    Field(result, "Column", a, b, "Length", a.Length, b.Length, oldColumnPath, newColumnPath);
                    Field(result, "Column", a, b, "Precision", a.Precision, b.Precision, oldColumnPath, newColumnPath);
                    Field(result, "Column", a, b, "Mandatory", a.Mandatory.ToString(), b.Mandatory.ToString(), oldColumnPath, newColumnPath);
                    Field(result, "Column", a, b, "DefaultValue", a.DefaultValue, b.DefaultValue, oldColumnPath, newColumnPath);
                    Field(result, "Column", a, b, "Identity", a.Identity.ToString(), b.Identity.ToString(), oldColumnPath, newColumnPath);
                    Field(result, "Column", a, b, "Description", a.Description, b.Description, oldColumnPath, newColumnPath);
                }
                CompareKeys(pair.Item1, pair.Item2, before, after, comparison, result, identities);
                CompareIndexes(pair.Item1, pair.Item2, before, after, comparison, result, identities);
                IdentityField(result, "Table", pair.Item1, pair.Item2, "PrimaryKey",
                    KeyColumnIdentities(pair.Item1, identities, false),
                    KeyColumnIdentities(pair.Item2, identities, true),
                    KeyColumns(pair.Item1, pair.Item1.PrimaryKeyId), KeyColumns(pair.Item2, pair.Item2.PrimaryKeyId),
                    TablePath(before, pair.Item1), TablePath(after, pair.Item2));
            }
            var views = Match(before.AllViews.ToArray(), after.AllViews.ToArray(), "View",
                x => ViewPath(before, x), x => ViewPath(after, x), comparison, result);
            foreach (var pair in views)
            {
                CompareCommon(pair.Item1, pair.Item2, "View", ViewPath(before, pair.Item1),
                    ViewPath(after, pair.Item2), result, comparison);
                Field(result, "View", pair.Item1, pair.Item2, "SQL", pair.Item1.ViewSQLQuery, pair.Item2.ViewSQLQuery,
                    ViewPath(before, pair.Item1), ViewPath(after, pair.Item2));
                Field(result, "View", pair.Item1, pair.Item2, "TaggedSQL", pair.Item1.TaggedSQLQuery, pair.Item2.TaggedSQLQuery,
                    ViewPath(before, pair.Item1), ViewPath(after, pair.Item2));
                Field(result, "View", pair.Item1, pair.Item2, "Description", pair.Item1.Description, pair.Item2.Description,
                    ViewPath(before, pair.Item1), ViewPath(after, pair.Item2));
            }
            var references = Match(before.AllReferences.ToArray(), after.AllReferences.ToArray(), "Reference",
                x => ReferencePath(before, x), x => ReferencePath(after, x), comparison, result);
            foreach (var pair in references)
            {
                var oldPath = ReferencePath(before, pair.Item1);
                var newPath = ReferencePath(after, pair.Item2);
                CompareCommon(pair.Item1, pair.Item2, "Reference", oldPath, newPath, result, comparison);
                Field(result, "Reference", pair.Item1, pair.Item2, "ForeignKeyConstraintName",
                    pair.Item1.ForeignKeyConstraintName, pair.Item2.ForeignKeyConstraintName, oldPath, newPath);
                IdentityField(result, "Reference", pair.Item1, pair.Item2, "ParentTable",
                    new[] { EndpointIdentity(before, pair.Item1.ParentTableId, pair.Item1.ParentTableAddress, identities, false, false) },
                    new[] { EndpointIdentity(after, pair.Item2.ParentTableId, pair.Item2.ParentTableAddress, identities, true, false) },
                    Endpoint(before, pair.Item1.ParentTableId, pair.Item1.ParentTableAddress),
                    Endpoint(after, pair.Item2.ParentTableId, pair.Item2.ParentTableAddress), oldPath, newPath);
                IdentityField(result, "Reference", pair.Item1, pair.Item2, "ChildTable",
                    new[] { EndpointIdentity(before, pair.Item1.ChildTableId, pair.Item1.ChildTableAddress, identities, false, false) },
                    new[] { EndpointIdentity(after, pair.Item2.ChildTableId, pair.Item2.ChildTableAddress, identities, true, false) },
                    Endpoint(before, pair.Item1.ChildTableId, pair.Item1.ChildTableAddress),
                    Endpoint(after, pair.Item2.ChildTableId, pair.Item2.ChildTableAddress), oldPath, newPath);
                IdentityField(result, "Reference", pair.Item1, pair.Item2, "Joins",
                    JoinIdentities(before, pair.Item1, identities, false),
                    JoinIdentities(after, pair.Item2, identities, true),
                    Joins(before, pair.Item1), Joins(after, pair.Item2), oldPath, newPath);
            }
            result.Changes.Sort((x, y) => string.CompareOrdinal(
                SortKey(x), SortKey(y)));
            return result;
        }

        /// <summary>
        /// 生成包含对象身份和值的确定性排序键。
        /// </summary>
        private static string SortKey(PdmDiffChange change) =>
            string.Join("\u001f", new[] { change.Kind, change.BeforePath, change.AfterPath,
                change.ChangeType, change.Property, change.BeforeId, change.AfterId,
                change.BeforeValue, change.AfterValue }.Select(x => x ?? string.Empty));

        /// <summary>
        /// 按唯一 GUID、再按路径和代码配对对象。
        /// </summary>
        private static List<Tuple<T, T>> Match<T>(T[] oldItems, T[] newItems, string kind,
            Func<T, string> oldPath, Func<T, string> newPath, StringComparison comparison, PdmDiffResult result,
            Func<T, string> oldDisplay = null, Func<T, string> newDisplay = null)
            where T : PdmCommonInfo
        {
            if (oldDisplay == null) oldDisplay = oldPath;
            if (newDisplay == null) newDisplay = newPath;
            var pairs = new List<Tuple<T, T>>();
            var unmatchedOld = new HashSet<T>(oldItems);
            var unmatchedNew = new HashSet<T>(newItems);
            foreach (var group in newItems.Where(x => !string.IsNullOrEmpty(x.ObjectId))
                .GroupBy(x => PdmIdentity.Normalize(x.ObjectId), StringComparer.Ordinal)
                .Where(x => x.Count() > 1 && !oldItems.Any(y =>
                    PdmIdentity.Equals(y.ObjectId, x.Key))))
            {
                foreach (var item in group) unmatchedNew.Remove(item);
                Change(result, kind, "Ambiguous", null, group.First(), null,
                    newDisplay(group.First()), "ObjectID", null, group.First().ObjectId);
            }
            foreach (var old in oldItems)
            {
                if (string.IsNullOrEmpty(old.ObjectId)) continue;
                var left = oldItems.Count(x => PdmIdentity.Equals(x.ObjectId, old.ObjectId));
                var right = newItems.Where(x => PdmIdentity.Equals(x.ObjectId, old.ObjectId)).ToArray();
                if (left > 1 || right.Length > 1)
                {
                    Change(result, kind, "Ambiguous", old, right.FirstOrDefault(), oldDisplay(old),
                        right.Length == 0 ? null : newDisplay(right[0]), "ObjectID", old.ObjectId, old.ObjectId);
                    unmatchedOld.Remove(old);
                    foreach (var item in right) unmatchedNew.Remove(item);
                }
                else if (right.Length == 1 && unmatchedNew.Remove(right[0]))
                {
                    unmatchedOld.Remove(old);
                    pairs.Add(Tuple.Create(old, right[0]));
                }
            }
            foreach (var old in unmatchedOld.ToArray())
            {
                if (!unmatchedOld.Contains(old)) continue;
                var matches = unmatchedNew.Where(x => string.Equals(newPath(x), oldPath(old), comparison)).ToArray();
                if (matches.Length == 1 && unmatchedOld.Count(x => string.Equals(oldPath(x), oldPath(old), comparison)) == 1)
                {
                    unmatchedOld.Remove(old); unmatchedNew.Remove(matches[0]);
                    pairs.Add(Tuple.Create(old, matches[0]));
                }
                else if (matches.Length > 1 || (matches.Length == 1 &&
                    unmatchedOld.Count(x => string.Equals(oldPath(x), oldPath(old), comparison)) > 1))
                {
                    Change(result, kind, "Ambiguous", old, null, oldDisplay(old), null,
                        "Path", oldPath(old), null);
                    unmatchedOld.Remove(old);
                    foreach (var item in matches) unmatchedNew.Remove(item);
                    foreach (var item in unmatchedOld.Where(x =>
                        string.Equals(oldPath(x), oldPath(old), comparison)).ToArray())
                        unmatchedOld.Remove(item);
                }
            }
            foreach (var old in unmatchedOld)
                Change(result, kind, "Removed", old, null, oldDisplay(old), null, null, null, null);
            foreach (var item in unmatchedNew)
                Change(result, kind, "Added", null, item, null, newDisplay(item), null, null, null);
            return pairs;
        }

        /// <summary>
        /// 比较通用名称、代码、注释和位置。
        /// </summary>
        private static void CompareCommon(PdmCommonInfo a, PdmCommonInfo b, string kind,
            string oldPath, string newPath, PdmDiffResult result, StringComparison comparison)
        {
            Field(result, kind, a, b, "Name", a.Name, b.Name, oldPath, newPath, comparison);
            Field(result, kind, a, b, "Code", a.Code, b.Code, oldPath, newPath, comparison);
            Field(result, kind, a, b, "Comment", a.Comment, b.Comment, oldPath, newPath);
            if (!string.Equals(oldPath, newPath, comparison))
                Field(result, kind, a, b, "Path", oldPath, newPath, oldPath, newPath);
        }

        /// <summary>
        /// 比较主键和候选键的有序列。
        /// </summary>
        private static void CompareKeys(TableInfo a, TableInfo b, PdmInfo before, PdmInfo after,
            StringComparison comparison, PdmDiffResult result, ComparisonIdentities identities)
        {
            foreach (var pair in Match(a.Keys.ToArray(), b.Keys.ToArray(), "Key",
                x => x.Code, x => x.Code, comparison, result,
                x => TablePath(before, a) + "." + x.Code,
                x => TablePath(after, b) + "." + x.Code))
            {
                var oldPath = TablePath(before, a) + "." + pair.Item1.Code;
                var newPath = TablePath(after, b) + "." + pair.Item2.Code;
                CompareCommon(pair.Item1, pair.Item2, "Key", oldPath, newPath, result, comparison);
                IdentityField(result, "Key", pair.Item1, pair.Item2, "Columns",
                    ColumnIdentities(a, pair.Item1.ColumnIds, identities, false),
                    ColumnIdentities(b, pair.Item2.ColumnIds, identities, true),
                    ColumnCodes(a, pair.Item1.ColumnIds), ColumnCodes(b, pair.Item2.ColumnIds),
                    oldPath, newPath);
            }
        }

        /// <summary>
        /// 比较索引唯一性和有序列。
        /// </summary>
        private static void CompareIndexes(TableInfo a, TableInfo b, PdmInfo before, PdmInfo after,
            StringComparison comparison, PdmDiffResult result, ComparisonIdentities identities)
        {
            foreach (var pair in Match(a.Indexes.ToArray(), b.Indexes.ToArray(), "Index",
                x => x.Code, x => x.Code, comparison, result,
                x => TablePath(before, a) + "." + x.Code,
                x => TablePath(after, b) + "." + x.Code))
            {
                var oldPath = TablePath(before, a) + "." + pair.Item1.Code;
                var newPath = TablePath(after, b) + "." + pair.Item2.Code;
                CompareCommon(pair.Item1, pair.Item2, "Index", oldPath, newPath, result, comparison);
                Field(result, "Index", pair.Item1, pair.Item2, "Unique",
                    pair.Item1.Unique.ToString(), pair.Item2.Unique.ToString(), oldPath, newPath);
                IdentityField(result, "Index", pair.Item1, pair.Item2, "Columns",
                    ColumnIdentities(a, pair.Item1.ColumnIds, identities, false),
                    ColumnIdentities(b, pair.Item2.ColumnIds, identities, true),
                    ColumnCodes(a, pair.Item1.ColumnIds), ColumnCodes(b, pair.Item2.ColumnIds),
                    oldPath, newPath);
            }
        }

        /// <summary>
        /// 记录变化的属性。
        /// </summary>
        private static void Field(PdmDiffResult result, string kind, PdmCommonInfo a, PdmCommonInfo b,
            string property, string oldValue, string newValue, string oldPath = null, string newPath = null,
            StringComparison comparison = StringComparison.Ordinal)
        {
            oldValue = Normalize(oldValue); newValue = Normalize(newValue);
            if (!string.Equals(oldValue, newValue, comparison))
                Change(result, kind, "Modified", a, b, oldPath ?? a.Code, newPath ?? b.Code,
                    property, oldValue, newValue);
        }

        /// <summary>
        /// 创建差异项。
        /// </summary>
        private static void Change(PdmDiffResult result, string kind, string type, PdmCommonInfo a,
            PdmCommonInfo b, string oldPath, string newPath, string property, string oldValue, string newValue) =>
            result.Changes.Add(new PdmDiffChange { Kind = kind, ChangeType = type,
                BeforeId = a?.ObjectId ?? a?.Id, AfterId = b?.ObjectId ?? b?.Id,
                BeforePath = oldPath, AfterPath = newPath, Property = property,
                BeforeValue = oldValue, AfterValue = newValue });

        /// <summary>
        /// 统一文本换行。
        /// </summary>
        private static string Normalize(string value) => value?.Replace("\r\n", "\n").Replace('\r', '\n');

        /// <summary>
        /// 解析表路径。
        /// </summary>
        private static string TablePath(PdmInfo model, TableInfo table) =>
            PackagePath(model, table.PackageId) + "/" +
            (model.Schemas.FirstOrDefault(x => x.Id == table.SchemaId)?.Code ?? table.SchemaId ?? string.Empty) +
            "/" + table.Code;

        /// <summary>
        /// 解析视图路径。
        /// </summary>
        private static string ViewPath(PdmInfo model, ViewInfo view) =>
            PackagePath(model, view.PackageId) + "/" + view.Code;

        /// <summary>
        /// 从包层级解析引用路径。
        /// </summary>
        private static string ReferencePath(PdmInfo model, ReferenceInfo reference)
        {
            foreach (var package in model.Packages)
            {
                var path = ReferencePackagePath(package, reference, package.Code);
                if (path != null) return path + "/" + reference.Code;
            }
            return "/" + reference.Code;
        }

        /// <summary>
        /// 查找引用所属的包路径。
        /// </summary>
        private static string ReferencePackagePath(PackageInfo package, ReferenceInfo reference, string path)
        {
            if (package.References.Contains(reference)) return path;
            foreach (var child in package.Packages)
            {
                var found = ReferencePackagePath(child, reference, path + "/" + child.Code);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 从包 ID 取得代码路径。
        /// </summary>
        private static string PackagePath(PdmInfo model, string id)
        {
            foreach (var package in model.Packages)
            {
                var found = PackagePath(package, id, package.Code);
                if (found != null) return found;
            }
            return string.Empty;
        }

        /// <summary>
        /// 递归解析包路径。
        /// </summary>
        private static string PackagePath(PackageInfo package, string id, string path)
        {
            if (package.Id == id) return path;
            foreach (var child in package.Packages)
            {
                var found = PackagePath(child, id, path + "/" + child.Code);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 按代码表示有序列。
        /// </summary>
        private static string ColumnCodes(TableInfo table, IEnumerable<string> ids) =>
            string.Join(",", ids.Select(id => table.Columns.FirstOrDefault(x => x.Id == id) is ColumnInfo column
                ? ColumnIdentity(column) : "#" + id));

        /// <summary>
        /// 使用稳定身份表示列。
        /// </summary>
        private static string ColumnIdentity(ColumnInfo column) => column.ObjectId ?? column.Code ?? column.Id;

        /// <summary>
        /// 取得主键的有序列。
        /// </summary>
        private static string KeyColumns(TableInfo table, string id) =>
            ColumnCodes(table, table.Keys.FirstOrDefault(x => x.Id == id)?.ColumnIds ?? new List<string>());

        /// <summary>
        /// 将端点地址规范化为模型键与对象身份。
        /// </summary>
        private static string Endpoint(PdmInfo model, string id, PdmObjectAddress address)
        {
            if (address != null) return address.ModelKey + ":" + (address.ObjectId ?? address.PdmId);
            return model.AllTables.FirstOrDefault(x => x.Id == id)?.ObjectId ?? id;
        }

        /// <summary>
        /// 将外键列关联规范化为有序表示。
        /// </summary>
        private static string Joins(PdmInfo model, ReferenceInfo reference) => string.Join(",",
            reference.Joins.Select(x =>
                ColumnEndpoint(model, x.ParentColumnId, x.ParentColumnAddress) +
                "=" + ColumnEndpoint(model, x.ChildColumnId, x.ChildColumnAddress)));

        /// <summary>
        /// 按稳定对象身份表示外键列端点。
        /// </summary>
        private static string ColumnEndpoint(PdmInfo model, string id, PdmObjectAddress address)
        {
            if (address != null) return address.ModelKey + ":" + (address.ObjectId ?? address.PdmId);
            return model.AllTables.SelectMany(x => x.Columns)
                .FirstOrDefault(x => x.Id == id)?.ObjectId ?? id;
        }

        /// <summary>
        /// 按配对身份比较关系并保留两侧原始值。
        /// </summary>
        private static void IdentityField(PdmDiffResult result, string kind, PdmCommonInfo a, PdmCommonInfo b,
            string property, IEnumerable<string> oldIdentities, IEnumerable<string> newIdentities,
            string oldValue, string newValue, string oldPath, string newPath)
        {
            if (!oldIdentities.SequenceEqual(newIdentities, StringComparer.Ordinal))
                Change(result, kind, "Modified", a, b, oldPath, newPath, property, oldValue, newValue);
        }

        /// <summary>
        /// 取得有序列的配对身份。
        /// </summary>
        private static IEnumerable<string> ColumnIdentities(TableInfo table, IEnumerable<string> ids,
            ComparisonIdentities identities, bool after) => ids.Select(id =>
                table.Columns.FirstOrDefault(x => x.Id == id) is ColumnInfo column
                    ? identities.Get(column, after) : "unresolved:" + id);

        /// <summary>
        /// 取得主键列的配对身份。
        /// </summary>
        private static IEnumerable<string> KeyColumnIdentities(TableInfo table, ComparisonIdentities identities, bool after) =>
            ColumnIdentities(table, table.Keys.FirstOrDefault(x => x.Id == table.PrimaryKeyId)?.ColumnIds
                ?? new List<string>(), identities, after);

        /// <summary>
        /// 取得端点的配对身份。
        /// </summary>
        private static string EndpointIdentity(PdmInfo model, string id, PdmObjectAddress address,
            ComparisonIdentities identities, bool after, bool column)
        {
            IEnumerable<PdmCommonInfo> candidates = column
                ? model.AllTables.SelectMany(x => x.Columns).Cast<PdmCommonInfo>() : model.AllTables;
            var local = candidates.FirstOrDefault(x => x.Id == (address?.PdmId ?? id));
            if (address == null) return local == null ? "unresolved:" + id : identities.Get(local, after);
            var workspace = after ? identities.Options?.AfterWorkspace : identities.Options?.BeforeWorkspace;
            var currentKey = after ? identities.Options?.AfterModelKey : identities.Options?.BeforeModelKey;
            if (workspace != null)
            {
                var key = currentKey ?? workspace.Models.FirstOrDefault(x => ReferenceEquals(x.Value, model)).Key;
                if (address.ModelKey == key && local != null) return identities.Get(local, after);
                if (workspace.TryGetModel(address.ModelKey, out var dependency))
                {
                    IEnumerable<PdmCommonInfo> external = column
                        ? dependency.AllTables.SelectMany(x => x.Columns).Cast<PdmCommonInfo>() : dependency.AllTables;
                    var target = external.FirstOrDefault(x => x.Id == address.PdmId);
                    return "external:" + Newtonsoft.Json.JsonConvert.SerializeObject(new[]
                        { PdmIdentity.Normalize(dependency.ObjectId),
                            PdmIdentity.Normalize(address.ObjectId ?? target?.ObjectId) ?? address.PdmId });
                }
            }
            if (local != null && address.PdmId == local.Id &&
                !string.IsNullOrEmpty(address.ObjectId) &&
                PdmIdentity.Equals(address.ObjectId, local.ObjectId))
                return identities.Get(local, after);
            return "external:" + Newtonsoft.Json.JsonConvert.SerializeObject(new[]
                { address.ModelKey, PdmIdentity.Normalize(address.ObjectId) ?? address.PdmId });
        }

        /// <summary>
        /// 取得外键列关联的有序配对身份。
        /// </summary>
        private static IEnumerable<string> JoinIdentities(PdmInfo model, ReferenceInfo reference,
            ComparisonIdentities identities, bool after)
        {
            foreach (var join in reference.Joins)
            {
                yield return EndpointIdentity(model, join.ParentColumnId, join.ParentColumnAddress, identities, after, true);
                yield return EndpointIdentity(model, join.ChildColumnId, join.ChildColumnAddress, identities, after, true);
            }
        }

        /// <summary>
        /// 一次比较中共享的对象配对身份。
        /// </summary>
        private sealed class ComparisonIdentities
        {
            /// <summary>
            /// 保存对象与配对身份的映射。
            /// </summary>
            private readonly Dictionary<PdmCommonInfo, string> _identities = new Dictionary<PdmCommonInfo, string>();

            /// <summary>
            /// 获取本次比较上下文。
            /// </summary>
            public PdmCompareOptions Options { get; }

            /// <summary>
            /// 初始化一个 <see cref="ComparisonIdentities"/> 类型的实例。
            /// </summary>
            public ComparisonIdentities(PdmCompareOptions options) { Options = options; }

            /// <summary>
            /// 为配对对象登记共同身份。
            /// </summary>
            public void Add<T>(IEnumerable<Tuple<T, T>> pairs) where T : PdmCommonInfo
            {
                foreach (var pair in pairs)
                {
                    var identity = "matched:" + _identities.Count;
                    _identities[pair.Item1] = identity;
                    _identities[pair.Item2] = identity;
                }
            }

            /// <summary>
            /// 取得对象在本次比较中的身份。
            /// </summary>
            public string Get(PdmCommonInfo value, bool after) =>
                _identities.TryGetValue(value, out var identity) ? identity
                    : (after ? "after:" : "before:") + (value.ObjectId ?? value.Id ?? value.Code);
        }
    }
}
