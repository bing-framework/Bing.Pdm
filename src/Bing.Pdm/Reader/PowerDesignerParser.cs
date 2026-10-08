using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Bing.Pdm.Models;
using Bing.Pdm.Models.Keys;
using Bing.Pdm.Models.PhysicalDiagrams;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Models.Views;

namespace Bing.Pdm.Reader
{
    /// <summary>
    /// 解析 PowerDesigner PDM XML 文档。
    /// </summary>
    internal sealed partial class PowerDesignerParser
    {
        /// <summary>
        /// XML 属性命名空间。
        /// </summary>
        private const string AttributeNs = "attribute";
        /// <summary>
        /// XML 集合命名空间。
        /// </summary>
        private const string CollectionNs = "collection";
        /// <summary>
        /// XML 对象命名空间。
        /// </summary>
        private const string ObjectNs = "object";
        /// <summary>
        /// 在解析期间提取几何文本中的有符号整数坐标。
        /// </summary>
        private static readonly Regex Coordinates = new Regex(@"[+-]?\d+", RegexOptions.Compiled);

        /// <summary>
        /// 从流解析 PDM 模型。
        /// </summary>
        /// <param name="stream">包含 PDM XML 的输入流。</param>
        /// <returns>解析后的 PDM 模型。</returns>
        /// <remarks>
        /// 解析过程中不会关闭输入流。
        /// </remarks>
        public PdmInfo Parse(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = false };
            var document = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(stream, settings))
                document.Load(reader);

            var model = document.GetElementsByTagName("Model", ObjectNs).OfType<XmlElement>().FirstOrDefault();
            if (model == null) throw new InvalidDataException("The PDM document contains no o:Model node.");

            var result = new PdmInfo();
            ReadCommon(model, result);
            result.Author = Value(model, "Author");
            result.Version = Value(model, "Version");
            result.DisplayPreferences = Value(model, "DisplayPreferences");
            result.RepositoryFileName = Value(model, "RepositoryFilename");
            var dbms = Elements(Child(model, CollectionNs, "DBMS")).FirstOrDefault();
            result.DbmsName = Value(dbms, "Name");
            result.DbmsCode = Value(dbms, "Code");
            ReadOwners(model, result);
            ReadContent(model, null, result.Packages, result.Tables, result.Shortcuts, result.Views, result.References,
                result.PhysicalDiagrams, result.Diagnostics);
            ReadMetadata(model, result.TargetModels, result.Replications, result.SubReplications);
            BindSubReplicationRefs(result);
            ResolveShortcuts(result);
            Validate(result);
            ValidateMetadata(result, model);
            return result;
        }

        /// <summary>
        /// 读取节点中的包、表、视图、引用和物理图。
        /// </summary>
        /// <param name="node">当前模型或包节点。</param>
        /// <param name="packageId">当前对象所属包标识。</param>
        /// <param name="packages">包结果集合。</param>
        /// <param name="tables">表结果集合。</param>
        /// <param name="shortcuts">Shortcut 结果集合。</param>
        /// <param name="views">视图结果集合。</param>
        /// <param name="references">引用结果集合。</param>
        /// <param name="diagrams">物理图结果集合。</param>
        /// <param name="diagnostics">读取诊断结果集合。</param>
        private static void ReadContent(XmlElement node, string packageId, List<PackageInfo> packages,
            List<TableInfo> tables, List<PdmShortcutInfo> shortcuts, List<ViewInfo> views, List<ReferenceInfo> references,
            List<PhysicalDiagramInfo> diagrams, List<PdmDiagnostic> diagnostics)
        {
            foreach (var element in Objects(node, "Packages", "Package"))
            {
                var package = new PackageInfo();
                ReadCommon(element, package);
                ReadContent(element, package.Id, package.Packages, package.Tables, package.Shortcuts, package.Views,
                    package.References, package.PhysicalDiagrams, diagnostics);
                ReadMetadata(element, package.TargetModels, package.Replications, package.SubReplications);
                packages.Add(package);
            }
            foreach (var element in Objects(node, "Tables", "Table"))
            {
                tables.Add(ReadTable(element, packageId));
                foreach (var item in Objects(element, "Columns", "Shortcut"))
                    shortcuts.Add(ReadShortcut(item, packageId, "Column"));
                foreach (var item in Objects(element, "Keys", "Shortcut"))
                    shortcuts.Add(ReadShortcut(item, packageId, "Key"));
            }
            foreach (var container in new[] { "Packages", "Tables", "Views", "References" })
                foreach (var element in Objects(node, container, "Shortcut"))
                    shortcuts.Add(ReadShortcut(element, packageId,
                        container == "Packages" ? "Package" : container == "Tables" ? "Table" :
                        container == "Views" ? "View" : "Reference"));
            foreach (var element in Objects(node, "Views", "View"))
                views.Add(ReadView(element, packageId));
            foreach (var element in Objects(node, "References", "Reference"))
                references.Add(ReadReference(element));
            foreach (var element in Objects(node, "PhysicalDiagrams", "PhysicalDiagram"))
                diagrams.Add(ReadDiagram(element, packageId, diagnostics));
        }

        /// <summary>
        /// 读取一个业务对象 Shortcut。
        /// </summary>
        private static PdmShortcutInfo ReadShortcut(XmlElement element, string packageId, string defaultKind)
        {
            var classId = Value(element, "TargetClassID");
            var kind = ShortcutKind(classId);
            if (string.IsNullOrEmpty(classId) || kind == classId) kind = defaultKind;
            var shortcut = new PdmShortcutInfo
            {
                PackageId = packageId,
                TargetId = Value(element, "TargetID"),
                TargetClassId = classId,
                TargetPackagePath = Value(element, "TargetPackagePath"),
                TargetKind = kind
            };
            ReadCommon(element, shortcut);
            return shortcut;
        }

        /// <summary>
        /// 读取模型所有者和模式信息。
        /// </summary>
        /// <param name="modelNode">模型 XML 节点。</param>
        /// <param name="model">接收所有者和模式的模型。</param>
        private static void ReadOwners(XmlElement modelNode, PdmInfo model)
        {
            foreach (var ownerNode in Objects(modelNode, "Users", "User"))
            {
                var owner = new PdmOwnerInfo { Stereotype = Value(ownerNode, "Stereotype") };
                ReadCommon(ownerNode, owner);
                model.Owners.Add(owner);
                if (string.Equals(owner.Stereotype, "Schema", StringComparison.OrdinalIgnoreCase))
                {
                    model.Schemas.Add(new SchemaInfo
                    {
                        Id = owner.Id,
                        ObjectId = owner.ObjectId,
                        Name = owner.Name,
                        Code = owner.Code,
                        Comment = owner.Comment,
                        Creator = owner.Creator,
                        Modifier = owner.Modifier,
                        CreationDate = owner.CreationDate,
                        ModificationDate = owner.ModificationDate,
                        SchemaId = owner.Id,
                        StereoType = owner.Stereotype
                    });
                }
            }
        }

        /// <summary>
        /// 读取表及其列、键和索引。
        /// </summary>
        /// <param name="node">表 XML 节点。</param>
        /// <param name="packageId">所属包标识。</param>
        /// <returns>解析后的表信息。</returns>
        private static TableInfo ReadTable(XmlElement node, string packageId)
        {
            var table = new TableInfo
            {
                PackageId = packageId,
                OwnerId = Ref(node, "Owner", "User"),
                PrimaryKeyId = Ref(node, "PrimaryKey", "Key")
            };
            ReadCommon(node, table);
            table.Description = Value(node, "Description");
            foreach (var columnNode in Objects(node, new[] { "Columns", "ColumnInfos" }, "Column"))
            {
                var column = new ColumnInfo
                {
                    Description = Value(columnNode, "Description"),
                    DataType = Value(columnNode, "DataType"),
                    Length = Value(columnNode, "Length"),
                    Precision = Value(columnNode, "Precision"),
                    DefaultValue = Value(columnNode, "DefaultValue"),
                    Mandatory = Boolean(Value(columnNode, "Column.Mandatory")),
                    Identity = Boolean(Value(columnNode, "Identity"))
                };
                ReadCommon(columnNode, column);
                table.Columns.Add(column);
            }
            foreach (var keyNode in Objects(node, new[] { "Keys", "KeyInfos" }, "Key"))
            {
                var key = new KeyInfo();
                ReadCommon(keyNode, key);
                key.ColumnIds.AddRange(Refs(keyNode, "Key.Columns", "Key.ColumnInfos"));
                table.Keys.Add(key);
            }
            foreach (var indexNode in Objects(node, new[] { "Indexes", "IndexeInfos" }, "Index"))
            {
                var index = new IndexInfo { Unique = Boolean(Value(indexNode, "Unique")) };
                ReadCommon(indexNode, index);
                index.ColumnIds.AddRange(Refs(indexNode, "Index.Columns", "Index.ColumnInfos"));
                foreach (var indexColumnNode in Objects(indexNode, "IndexColumns", "IndexColumn"))
                {
                    var indexColumn = new IndexColumnInfo { ColumnId = Ref(indexColumnNode, "Column", "Column") };
                    ReadCommon(indexColumnNode, indexColumn);
                    index.IndexColumns.Add(indexColumn);
                    if (!string.IsNullOrEmpty(indexColumn.ColumnId)) index.ColumnIds.Add(indexColumn.ColumnId);
                }
                table.Indexes.Add(index);
            }
            foreach (var triggerNode in Objects(node, "Triggers", "Trigger"))
            {
                var trigger = new PdmTriggerInfo
                {
                    TableId = table.Id,
                    Timing = Value(triggerNode, "Time") ?? Value(triggerNode, "Timing"),
                    Event = Value(triggerNode, "Event"),
                    Body = Value(triggerNode, "Text") ?? Value(triggerNode, "Body")
                };
                ReadCommon(triggerNode, trigger);
                ReadUnknownAttributes(triggerNode, trigger.RawAttributes, "Time", "Timing", "Event", "Text", "Body");
                table.Triggers.Add(trigger);
            }
            return table;
        }

        /// <summary>
        /// 读取视图及其列。
        /// </summary>
        /// <param name="node">视图 XML 节点。</param>
        /// <param name="packageId">所属包标识。</param>
        /// <returns>解析后的视图信息。</returns>
        private static ViewInfo ReadView(XmlElement node, string packageId)
        {
            var view = new ViewInfo
            {
                PackageId = packageId,
                Description = Value(node, "Description"),
                ViewSQLQuery = Value(node, "View.SQLQuery"),
                TaggedSQLQuery = Value(node, "TaggedSQLQuery")
            };
            ReadCommon(node, view);
            foreach (var columnNode in Objects(node, new[] { "Columns", "ColumnInfos" }, "Column"))
            {
                var column = new ViewColumnInfo
                {
                    ViewId = view.Id,
                    Description = Value(columnNode, "Description"),
                    DataType = Value(columnNode, "DataType"),
                    Length = Value(columnNode, "Length"),
                    Precision = Value(columnNode, "Precision"),
                    DefaultValue = Value(columnNode, "DefaultValue"),
                    Mandatory = Boolean(Value(columnNode, "Column.Mandatory"))
                };
                ReadCommon(columnNode, column);
                view.Columns.Add(column);
            }
            return view;
        }

        /// <summary>
        /// 读取表引用及其列关联。
        /// </summary>
        /// <param name="node">引用 XML 节点。</param>
        /// <returns>解析后的引用信息。</returns>
        private static ReferenceInfo ReadReference(XmlElement node)
        {
            var reference = new ReferenceInfo
            {
                ParentTableId = ObjectRef(node, "ParentTable"),
                ChildTableId = ObjectRef(node, "ChildTable"),
                ParentKeyId = Ref(node, "ParentKey", "Key"),
                Cardinality = Value(node, "Cardinality"),
                ForeignKeyConstraintName = Value(node, "ForeignKeyConstraintName")
            };
            ReadCommon(node, reference);
            reference.RawParentTableRef = reference.ParentTableId;
            reference.RawChildTableRef = reference.ChildTableId;
            foreach (var joinNode in Objects(node, "Joins", "ReferenceJoin"))
            {
                var join = new ReferenceJoinInfo
                {
                    ParentColumnId = Ref(joinNode, "Object1", "Column"),
                    ChildColumnId = Ref(joinNode, "Object2", "Column")
                };
                ReadCommon(joinNode, join);
                reference.Joins.Add(join);
            }
            return reference;
        }

        /// <summary>
        /// 读取物理图及其图形符号。
        /// </summary>
        /// <param name="node">物理图 XML 节点。</param>
        /// <param name="packageId">所属包标识。</param>
        /// <param name="diagnostics">接收图形符号诊断的集合。</param>
        /// <returns>解析后的物理图信息。</returns>
        private static PhysicalDiagramInfo ReadDiagram(XmlElement node, string packageId, List<PdmDiagnostic> diagnostics)
        {
            var diagram = new PhysicalDiagramInfo { PackageId = packageId };
            ReadCommon(node, diagram);
            diagram.DisplayPreferences = Value(node, "DisplayPreferences");
            var symbols = Child(node, CollectionNs, "Symbols");
            foreach (var symbolNode in Elements(symbols))
            {
                if (symbolNode.NamespaceURI != ObjectNs || !symbolNode.HasAttribute("Id")) continue;
                var symbol = ReadSymbol(symbolNode, diagnostics);
                if (symbol == null) continue;
                if (symbol.Kind == "ArchitectureAreaSymbol" && symbol.Text == null &&
                    symbol.ObjectId != null)
                {
                    var area = node.OwnerDocument.GetElementsByTagName("Area", ObjectNs)
                        .OfType<XmlElement>().FirstOrDefault(x => x.GetAttribute("Id") == symbol.ObjectId);
                    symbol.Text = Value(area, "Name") ?? Value(area, "Code");
                }
                diagram.Symbols.Add(symbol);
            }
            return diagram;
        }

        /// <summary>
        /// 可解析的 PowerDesigner 物理图符号名称。
        /// </summary>
        private static readonly HashSet<string> SupportedSymbols = new HashSet<string>(StringComparer.Ordinal)
        {
            "TableSymbol", "ReferenceSymbol", "PackageSymbol", "NoteSymbol", "NoteLinkSymbol",
            "TextSymbol", "EllipseSymbol", "PolylineSymbol", "PredefinedSymbol",
            "ArchitectureAreaSymbol", "ExtendedDependencySymbol"
        };

        /// <summary>
        /// 读取图形符号及其嵌套符号。
        /// </summary>
        /// <param name="node">图形符号 XML 节点。</param>
        /// <param name="diagnostics">接收几何及符号诊断的集合。</param>
        /// <returns>解析后的图形符号；不支持的类型保留为不可渲染容器。</returns>
        private static DiagramSymbolInfo ReadSymbol(XmlElement node, List<PdmDiagnostic> diagnostics)
        {
            var kind = node.LocalName;
            var rectText = Value(node, "Rect");
            var pointText = Value(node, "ListOfPoints");
            if (!SupportedSymbols.Contains(kind) &&
                (!string.IsNullOrWhiteSpace(rectText) || !string.IsNullOrWhiteSpace(pointText)))
                diagnostics.Add(Diagnostic("UNSUPPORTED_DIAGRAM_SYMBOL", node.GetAttribute("Id"), "Visible diagram symbol type '" + kind + "' is not supported."));
            var rect = Rectangle(rectText, out var rectValid);
            if (!string.IsNullOrWhiteSpace(rectText) && !rectValid)
                diagnostics.Add(Diagnostic("MALFORMED_GEOMETRY", node.GetAttribute("Id"), "Rectangle coordinates must contain four valid integers."));
            var pointValues = CoordinateValues(pointText, out var pointsValid);
            if (!string.IsNullOrWhiteSpace(pointText) && (!pointsValid || pointValues.Length < 4 || pointValues.Length % 2 != 0))
            {
                diagnostics.Add(Diagnostic("MALFORMED_GEOMETRY", node.GetAttribute("Id"), "Connector points must contain at least two valid coordinate pairs."));
                pointValues = new int[0];
            }
            var symbol = new DiagramSymbolInfo
            {
                Id = node.GetAttribute("Id"),
                Kind = kind,
                ObjectId = ObjectRef(node, "Object"),
                RawObjectRef = ObjectRef(node, "Object"),
                SourceSymbolId = ObjectRef(node, "SourceSymbol"),
                DestinationSymbolId = ObjectRef(node, "DestinationSymbol"),
                Text = RtfText.Decode(Value(node, "Text")),
                RawText = Value(node, "Text"),
                SymbolType = Value(node, "SymbolType"),
                LineColor = Color(Value(node, "LineColor")),
                FillColor = Color(Value(node, "FillColor")),
                ShadowColor = Color(Value(node, "ShadowColor")),
                FontList = Value(node, "FontList"),
                FontName = Value(node, "FontName"),
                TextStyle = Value(node, "TextStyle"),
                DashStyle = Value(node, "DashStyle"),
                DisplayPreferences = Value(node, "DisplayPreferences"),
                CornerStyle = Value(node, "CornerStyle"),
                ArrowStyle = Value(node, "ArrowStyle"),
                PenStyle = Value(node, "PenStyle"),
                LineWidth = Value(node, "LineWidth"),
                BrushStyle = Value(node, "BrushStyle"),
                GradientFillMode = Value(node, "GradientFillMode"),
                GradientEndColor = Color(Value(node, "GradientEndColor")),
                Rect = rect
            };
            symbol.RichTextSegments.AddRange(RtfText.DecodeSegments(symbol.RawText));
            var siblings = node.ParentNode?.ChildNodes.OfType<XmlElement>()
                .Where(x => x.NamespaceURI == ObjectNs).ToList();
            symbol.DrawOrder = siblings?.IndexOf(node) ?? 0;
            for (var i = 0; i + 1 < pointValues.Length; i += 2)
                symbol.Points.Add(new DiagramPointInfo { X = pointValues[i], Y = pointValues[i + 1] });
            foreach (var child in Elements(Child(node, CollectionNs, "SubSymbols")))
            {
                if (child.NamespaceURI != ObjectNs || !child.HasAttribute("Id")) continue;
                var nested = ReadSymbol(child, diagnostics);
                if (nested != null) symbol.SubSymbols.Add(nested);
            }
            return symbol;
        }

        /// <summary>
        /// 将 PowerDesigner 颜色值转换为 CSS 颜色。
        /// </summary>
        /// <param name="value">PowerDesigner 颜色整数值。</param>
        /// <returns>转换后的 RGB 十六进制颜色；输入无效时返回 <see langword="null"/>。</returns>
        private static string Color(string value)
        {
            int color;
            if (!int.TryParse(value, out color) || color < 0 || color > 0xffffff) return null;
            return "#" + (color & 0xff).ToString("x2") + ((color >> 8) & 0xff).ToString("x2") + ((color >> 16) & 0xff).ToString("x2");
        }

        /// <summary>
        /// 根据目标类型标识推断 Shortcut 类型。
        /// </summary>
        /// <param name="targetClassId">Shortcut 目标类标识。</param>
        /// <returns>推断出的目标类型；无法识别时返回原始类标识。</returns>
        private static string ShortcutKind(string targetClassId)
        {
            if (string.IsNullOrWhiteSpace(targetClassId)) return "Table";
            var value = targetClassId.Trim();
            if (string.Equals(value, "380BAA40-0042-11D2-BD28-00A02478ECC9", StringComparison.OrdinalIgnoreCase)) return "Table";
            if (string.Equals(value, "380BAA42-0042-11D2-BD28-00A02478ECC9", StringComparison.OrdinalIgnoreCase)) return "View";
            if (value.IndexOf("VIEW", StringComparison.OrdinalIgnoreCase) >= 0) return "View";
            if (value.IndexOf("PACKAGE", StringComparison.OrdinalIgnoreCase) >= 0) return "Package";
            if (value.IndexOf("TABLE", StringComparison.OrdinalIgnoreCase) >= 0) return "Table";
            if (value.IndexOf("COLUMN", StringComparison.OrdinalIgnoreCase) >= 0) return "Column";
            if (value.IndexOf("KEY", StringComparison.OrdinalIgnoreCase) >= 0) return "Key";
            if (value.IndexOf("REFERENCE", StringComparison.OrdinalIgnoreCase) >= 0) return "Reference";
            return value;
        }

        /// <summary>
        /// 解析图形矩形坐标。
        /// </summary>
        /// <param name="text">包含矩形坐标的文本。</param>
        /// <param name="valid">坐标格式有效时为 <see langword="true"/>，否则为 <see langword="false"/>。</param>
        /// <returns>包含四个有效坐标的矩形；格式无效时返回 <see langword="null"/>。</returns>
        private static DiagramRectangleInfo Rectangle(string text, out bool valid)
        {
            var values = CoordinateValues(text, out valid);
            valid = valid && values.Length == 4;
            return valid
                ? new DiagramRectangleInfo { X1 = values[0], Y1 = values[1], X2 = values[2], Y2 = values[3] }
                : null;
        }

        /// <summary>
        /// 解析几何文本中的整数坐标。
        /// </summary>
        /// <param name="text">包含整数坐标的几何文本。</param>
        /// <param name="valid">文本仅包含允许的分隔符和有效整数时为 <see langword="true"/>。</param>
        /// <returns>按文本顺序解析出的坐标；文本为空或整数溢出时返回空数组。</returns>
        private static int[] CoordinateValues(string text, out bool valid)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                valid = false;
                return new int[0];
            }
            var remainder = Coordinates.Replace(text, string.Empty);
            var matches = Coordinates.Matches(text).Cast<Match>().ToArray();
            var values = new List<int>(matches.Length);
            valid = Regex.IsMatch(remainder, @"^[\s(),]*$");
            foreach (var match in matches)
            {
                int value;
                if (!int.TryParse(match.Value, out value))
                {
                    valid = false;
                    return new int[0];
                }
                values.Add(value);
            }
            return values.ToArray();
        }

        /// <summary>
        /// 解析 Shortcut 目标并规范化关系和图形引用。
        /// </summary>
        /// <param name="model">待解析引用目标的 PDM 模型。</param>
        private static void ResolveShortcuts(PdmInfo model)
        {
            var targets = CommonObjects(model).Where(x => !string.IsNullOrEmpty(x.ObjectId))
                .ToArray();
            var byId = model.AllShortcuts.Where(x => !string.IsNullOrEmpty(x.Id))
                .GroupBy(x => x.Id, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
            foreach (var shortcut in model.AllShortcuts)
            {
                var matches = targets.Where(x => x.GetType().Name == shortcut.TargetKind + "Info" &&
                    PdmIdentity.Equals(x.ObjectId, shortcut.TargetId)).ToArray();
                if (matches.Length == 0)
                    model.Diagnostics.Add(Diagnostic("UNRESOLVED_SHORTCUT_TARGET", shortcut.Id,
                        "Shortcut target '" + shortcut.TargetId + "' cannot be found in the root model."));
                else if (matches.Length != 1)
                    model.Diagnostics.Add(Diagnostic("AMBIGUOUS_SHORTCUT_TARGET", shortcut.Id,
                        "Shortcut target '" + shortcut.TargetId + "' matches multiple objects."));
                else shortcut.ResolvedTargetId = matches[0].Id;
            }
            Func<string, string> normalize = id =>
            {
                PdmShortcutInfo shortcut;
                return id != null && byId.TryGetValue(id, out shortcut) && !string.IsNullOrEmpty(shortcut.ResolvedTargetId)
                    ? shortcut.ResolvedTargetId : id;
            };
            foreach (var reference in model.AllReferences)
            {
                reference.ParentTableId = normalize(reference.ParentTableId);
                reference.ChildTableId = normalize(reference.ChildTableId);
            }
            foreach (var symbol in model.AllPhysicalDiagrams.SelectMany(x => x.AllSymbols))
                if (symbol.Kind == "TableSymbol") symbol.ObjectId = normalize(symbol.ObjectId);
        }

        /// <summary>
        /// 校验模型引用并生成诊断信息。
        /// </summary>
        /// <param name="model">待校验的 PDM 模型。</param>
        private static void Validate(PdmInfo model)
        {
            var ids = CommonObjects(model).Where(x => !string.IsNullOrEmpty(x.Id)).Select(x => Tuple.Create(x.Id, x.GetType().Name))
                .Concat(model.AllPhysicalDiagrams.SelectMany(x => x.AllSymbols).Where(x => !string.IsNullOrEmpty(x.Id))
                    .Select(x => Tuple.Create(x.Id, x.Kind ?? "diagram symbol")));
            foreach (var duplicate in ids.GroupBy(x => x.Item1).Where(x => x.Count() > 1))
                model.Diagnostics.Add(Diagnostic("DUPLICATE_ID", duplicate.Key,
                    "PDM object ID is shared by: " + string.Join(", ", duplicate.Select(x => x.Item2).Distinct()) + "."));

            model.RebuildLookup();
            foreach (var table in model.AllTables)
            {
                if (!string.IsNullOrEmpty(table.OwnerId))
                {
                    PdmOwnerInfo owner;
                    Check(model, model.Lookup.TryGetOwner(table.OwnerId, out owner), table.OwnerId, table.Id, "owner", false);
                    if (owner != null && string.Equals(owner.Stereotype, "Schema", StringComparison.OrdinalIgnoreCase))
                        table.SchemaId = owner.Id;
                }
                if (!string.IsNullOrEmpty(table.PrimaryKeyId))
                {
                    KeyInfo primary;
                    var found = model.Lookup.TryGetKey(table.PrimaryKeyId, out primary) && table.Keys.Contains(primary);
                    Check(model, found, table.PrimaryKeyId, table.Id, "primary key", false);
                    if (found)
                        foreach (var column in table.Columns)
                            column.PrimaryKey = primary.ColumnIds.Contains(column.Id);
                }
                foreach (var key in table.Keys)
                    foreach (var id in key.ColumnIds)
                        Check(model, table.Columns.Any(x => x.Id == id), id, key.Id, "key column", true);
                foreach (var index in table.Indexes)
                {
                    foreach (var id in index.ColumnIds)
                        Check(model, table.Columns.Any(x => x.Id == id), id, index.Id, "index column", true);
                    foreach (var indexColumn in index.IndexColumns.Where(x => string.IsNullOrEmpty(x.ColumnId)))
                        Check(model, false, indexColumn.ColumnId, indexColumn.Id, "index column", true);
                }
            }
            foreach (var reference in model.AllReferences)
            {
                TableInfo parent;
                TableInfo child;
                var hasParent = model.Lookup.TryGetTable(reference.ParentTableId, out parent);
                var hasChild = model.Lookup.TryGetTable(reference.ChildTableId, out child);
                Check(model, hasParent, reference.ParentTableId, reference.Id, "parent table", true);
                Check(model, hasChild, reference.ChildTableId, reference.Id, "child table", true);
                KeyInfo parentKey;
                var hasParentKey = model.Lookup.TryGetKey(reference.ParentKeyId, out parentKey)
                    && hasParent && parent.Keys.Contains(parentKey);
                Check(model, hasParentKey, reference.ParentKeyId, reference.Id, "parent key", hasParent);
                foreach (var join in reference.Joins)
                {
                    Check(model, hasParent && parent.Columns.Any(x => x.Id == join.ParentColumnId), join.ParentColumnId, join.Id, "parent column", true);
                    Check(model, hasChild && child.Columns.Any(x => x.Id == join.ChildColumnId), join.ChildColumnId, join.Id, "child column", true);
                }
                if (hasParentKey && reference.Joins.Count > 0)
                {
                    var expected = new HashSet<string>(parentKey.ColumnIds, StringComparer.Ordinal);
                    var joinedParent = reference.Joins.Select(x => x.ParentColumnId).ToArray();
                    var joinedChild = reference.Joins.Select(x => x.ChildColumnId).ToArray();
                    if (expected.Count != parentKey.ColumnIds.Count || joinedParent.Length != expected.Count ||
                        joinedParent.Distinct(StringComparer.Ordinal).Count() != joinedParent.Length ||
                        joinedChild.Distinct(StringComparer.Ordinal).Count() != joinedChild.Length ||
                        !expected.SetEquals(joinedParent))
                        model.Diagnostics.Add(Diagnostic("FOREIGN_KEY_KEY_MISMATCH", reference.Id,
                            "Foreign key joins must map every parent key column exactly once to a distinct child column."));
                }
                else if (hasParentKey && parentKey.ColumnIds.Count > 0 && reference.Joins.Count == 0)
                    model.Diagnostics.Add(Diagnostic("FOREIGN_KEY_KEY_MISMATCH", reference.Id,
                        "Foreign key has no joins for its parent key columns."));
            }
            foreach (var diagram in model.AllPhysicalDiagrams)
            {
                foreach (var symbol in diagram.AllSymbols)
                {
                    if (symbol.Kind == "TableSymbol" || symbol.Kind == "PackageSymbol" || symbol.Kind == "ReferenceSymbol")
                        Check(model, SymbolTargetExists(model.Lookup, symbol), symbol.ObjectId, symbol.Id, "diagram object", true);
                    if (symbol.Kind != "ReferenceSymbol" && symbol.Kind != "NoteLinkSymbol" && symbol.Kind != "ExtendedDependencySymbol") continue;
                    Check(model, diagram.AllSymbols.Any(x => x.Id == symbol.SourceSymbolId), symbol.SourceSymbolId, symbol.Id, "source symbol", true);
                    Check(model, diagram.AllSymbols.Any(x => x.Id == symbol.DestinationSymbolId), symbol.DestinationSymbolId, symbol.Id, "destination symbol", true);
                }
            }
        }

        /// <summary>
        /// 枚举模型中的通用对象。
        /// </summary>
        /// <param name="model">待枚举的 PDM 模型。</param>
        /// <returns>按模型结构顺序枚举的通用对象。</returns>
        private static IEnumerable<PdmCommonInfo> CommonObjects(PdmInfo model)
        {
            yield return model;
            foreach (var owner in model.Owners) yield return owner;
            foreach (var shortcut in model.Shortcuts) yield return shortcut;
            foreach (var package in AllPackages(model.Packages))
            {
                yield return package;
                foreach (var shortcut in package.Shortcuts) yield return shortcut;
                foreach (var diagram in package.PhysicalDiagrams) yield return diagram;
                foreach (var table in package.Tables)
                {
                    yield return table;
                    foreach (var column in table.Columns) yield return column;
                    foreach (var key in table.Keys) yield return key;
                    foreach (var trigger in table.Triggers) yield return trigger;
                    foreach (var index in table.Indexes)
                    {
                        yield return index;
                        foreach (var indexColumn in index.IndexColumns) yield return indexColumn;
                    }
                }
                foreach (var view in package.Views)
                {
                    yield return view;
                    foreach (var column in view.Columns) yield return column;
                }
                foreach (var reference in package.References)
                {
                    yield return reference;
                    foreach (var join in reference.Joins) yield return join;
                }
            }
            foreach (var diagram in model.PhysicalDiagrams) yield return diagram;
            foreach (var table in model.Tables)
            {
                yield return table;
                foreach (var column in table.Columns) yield return column;
                foreach (var key in table.Keys) yield return key;
                foreach (var trigger in table.Triggers) yield return trigger;
                foreach (var index in table.Indexes)
                {
                    yield return index;
                    foreach (var indexColumn in index.IndexColumns) yield return indexColumn;
                }
            }
            foreach (var view in model.Views)
            {
                yield return view;
                foreach (var column in view.Columns) yield return column;
            }
            foreach (var reference in model.References)
            {
                yield return reference;
                foreach (var join in reference.Joins) yield return join;
            }
        }

        /// <summary>
        /// 递归枚举包及其嵌套包。
        /// </summary>
        /// <param name="packages">待枚举的包集合。</param>
        /// <returns>按深度优先顺序枚举的包。</returns>
        private static IEnumerable<PackageInfo> AllPackages(IEnumerable<PackageInfo> packages)
        {
            foreach (var package in packages)
            {
                yield return package;
                foreach (var child in AllPackages(package.Packages)) yield return child;
            }
        }

        /// <summary>
        /// 判断图形符号目标是否存在。
        /// </summary>
        /// <param name="lookup">PDM 对象索引。</param>
        /// <param name="symbol">待检查的图形符号。</param>
        /// <returns>符号引用的表、包或引用存在时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        private static bool SymbolTargetExists(PdmLookupIndex lookup, DiagramSymbolInfo symbol)
        {
            TableInfo table;
            PackageInfo package;
            ReferenceInfo reference;
            if (symbol.Kind == "TableSymbol") return lookup.TryGetTable(symbol.ObjectId, out table);
            if (symbol.Kind == "PackageSymbol") return lookup.TryGetPackage(symbol.ObjectId, out package);
            return lookup.TryGetReference(symbol.ObjectId, out reference);
        }

        /// <summary>
        /// 检查引用并在缺失时追加诊断。
        /// </summary>
        /// <param name="model">接收诊断的 PDM 模型。</param>
        /// <param name="found">目标是否已解析。</param>
        /// <param name="target">引用目标标识。</param>
        /// <param name="source">引用来源对象标识。</param>
        /// <param name="role">引用目标角色名称。</param>
        /// <param name="required">目标缺失时是否生成诊断。</param>
        private static void Check(PdmInfo model, bool found, string target, string source, string role, bool required)
        {
            if (found) return;
            if (string.IsNullOrEmpty(target) && !required) return;
            var missing = string.IsNullOrEmpty(target);
            var diagnostic = Diagnostic(missing ? "MISSING_REF" : "UNRESOLVED_REF", source,
                missing ? "Required " + role + " reference is missing."
                    : role + " reference '" + target + "' cannot be resolved.");
            diagnostic.TargetId = target;
            diagnostic.Role = role;
            model.Diagnostics.Add(diagnostic);
        }

        /// <summary>
        /// 创建诊断信息。
        /// </summary>
        /// <param name="code">诊断代码。</param>
        /// <param name="source">诊断来源对象标识。</param>
        /// <param name="message">诊断消息。</param>
        /// <returns>包含指定内容的诊断信息。</returns>
        private static PdmDiagnostic Diagnostic(string code, string source, string message) =>
            new PdmDiagnostic { Code = code, SourceId = source, Message = message };

        /// <summary>
        /// 读取对象的通用属性。
        /// </summary>
        /// <param name="node">对象 XML 节点。</param>
        /// <param name="value">接收通用属性的模型对象。</param>
        private static void ReadCommon(XmlElement node, PdmCommonInfo value)
        {
            value.Id = node.GetAttribute("Id");
            value.ObjectId = Value(node, "ObjectID");
            value.Name = Value(node, "Name");
            value.Code = Value(node, "Code");
            value.Comment = Value(node, "Comment");
            value.Creator = Value(node, "Creator");
            value.Modifier = Value(node, "Modifier");
            value.CreationDate = Date(Value(node, "CreationDate"));
            value.ModificationDate = Date(Value(node, "ModificationDate"));
        }

        /// <summary>
        /// 将 Unix 时间戳转换为 UTC 时间。
        /// </summary>
        /// <param name="text">以秒为单位的 Unix 时间戳文本。</param>
        /// <returns>转换后的 UTC 时间；文本无法解析时返回默认时间值。</returns>
        private static DateTime Date(string text)
        {
            long seconds;
            return long.TryParse(text, out seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : default(DateTime);
        }

        /// <summary>
        /// 解析布尔属性值。
        /// </summary>
        /// <param name="text">XML 中的布尔值文本。</param>
        /// <returns>文本为“1”“true”或“y”（忽略大小写）时为 <see langword="true"/>；其他值为 <see langword="false"/>。</returns>
        private static bool Boolean(string text) => text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "y", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 读取指定名称的 XML 属性值。
        /// </summary>
        /// <param name="node">待读取的 XML 节点。</param>
        /// <param name="name">属性元素名称。</param>
        /// <returns>属性文本；节点或属性不存在时返回 <see langword="null"/>。</returns>
        private static string Value(XmlElement node, string name) => Child(node, AttributeNs, name)?.InnerText;

        /// <summary>
        /// 读取集合中指定对象的引用。
        /// </summary>
        /// <param name="node">包含集合的 XML 节点。</param>
        /// <param name="collection">集合元素名称。</param>
        /// <param name="objectName">目标对象元素名称。</param>
        /// <returns>首个匹配对象的引用标识；未找到时返回 <see langword="null"/>。</returns>
        private static string Ref(XmlElement node, string collection, string objectName) =>
            Elements(Child(node, CollectionNs, collection)).FirstOrDefault(x => x.NamespaceURI == ObjectNs && x.LocalName == objectName)?.GetAttribute("Ref");

        /// <summary>
        /// 读取集合中的第一个对象引用。
        /// </summary>
        /// <param name="node">包含集合的 XML 节点。</param>
        /// <param name="collection">集合元素名称。</param>
        /// <returns>首个对象的引用标识；集合为空时返回 <see langword="null"/>。</returns>
        private static string ObjectRef(XmlElement node, string collection) =>
            Elements(Child(node, CollectionNs, collection)).FirstOrDefault(x => x.NamespaceURI == ObjectNs)?.GetAttribute("Ref");

        /// <summary>
        /// 读取多个集合中的对象引用。
        /// </summary>
        /// <param name="node">包含集合的 XML 节点。</param>
        /// <param name="collections">集合元素名称。</param>
        /// <returns>按集合顺序枚举的对象引用标识。</returns>
        private static IEnumerable<string> Refs(XmlElement node, params string[] collections) =>
            collections.SelectMany(x => Elements(Child(node, CollectionNs, x)))
                .Where(x => x.NamespaceURI == ObjectNs && x.HasAttribute("Ref"))
                .Select(x => x.GetAttribute("Ref"));

        /// <summary>
        /// 枚举指定集合中的指定类型对象。
        /// </summary>
        /// <param name="node">包含集合的 XML 节点。</param>
        /// <param name="collection">集合元素名称。</param>
        /// <param name="objectName">目标对象元素名称。</param>
        /// <returns>集合中匹配的对象节点。</returns>
        private static IEnumerable<XmlElement> Objects(XmlElement node, string collection, string objectName) =>
            Objects(node, new[] { collection }, objectName);

        /// <summary>
        /// 枚举指定集合中的指定类型对象。
        /// </summary>
        /// <param name="node">包含集合的 XML 节点。</param>
        /// <param name="collections">集合元素名称。</param>
        /// <param name="objectName">目标对象元素名称。</param>
        /// <returns>按集合顺序枚举的匹配对象节点。</returns>
        private static IEnumerable<XmlElement> Objects(XmlElement node, string[] collections, string objectName) =>
            collections.SelectMany(x => Elements(Child(node, CollectionNs, x)))
                .Where(x => x.NamespaceURI == ObjectNs && x.LocalName == objectName && x.HasAttribute("Id"));

        /// <summary>
        /// 读取指定命名空间和名称的子元素。
        /// </summary>
        /// <param name="node">父 XML 节点。</param>
        /// <param name="ns">子元素命名空间。</param>
        /// <param name="name">子元素名称。</param>
        /// <returns>首个匹配的子元素；未找到时返回 <see langword="null"/>。</returns>
        private static XmlElement Child(XmlElement node, string ns, string name) =>
            Elements(node).FirstOrDefault(x => x.NamespaceURI == ns && x.LocalName == name);

        /// <summary>
        /// 枚举节点的 XML 子元素。
        /// </summary>
        /// <param name="node">待枚举的 XML 节点。</param>
        /// <returns>节点的子元素；节点为 <see langword="null"/> 时返回空集合。</returns>
        private static IEnumerable<XmlElement> Elements(XmlElement node) =>
            node == null ? Enumerable.Empty<XmlElement>() : node.ChildNodes.OfType<XmlElement>();
    }
}
