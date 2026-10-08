using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Bing.Pdm.Models;
using Bing.Pdm.Models.PhysicalDiagrams;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;

namespace Bing.Pdm
{
    /// <summary>
    /// 将物理图渲染为离线 SVG。
    /// </summary>
    internal static class SvgDiagramRenderer
    {
        /// <summary>
        /// 将物理图写入 SVG。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="diagram">待渲染的物理图。</param>
        /// <param name="writer">SVG 输出。</param>
        public static void Write(PdmInfo model, PhysicalDiagramInfo diagram, TextWriter writer)
        {
            Write(model, diagram, writer, false);
        }

        /// <summary>
        /// 将物理图写入 SVG。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="diagram">待渲染的物理图。</param>
        /// <param name="writer">SVG 输出。</param>
        /// <param name="includeLinks">是否为可定位对象生成链接。</param>
        public static void Write(PdmInfo model, PhysicalDiagramInfo diagram, TextWriter writer, bool includeLinks)
        {
            Write(model, diagram, writer, includeLinks, null);
        }

        /// <summary>
        /// 按指定风格写入物理图。
        /// </summary>
        public static void Write(PdmInfo model, PhysicalDiagramInfo diagram, TextWriter writer,
            bool includeLinks, PdmDiagramRenderOptions options, PdmWorkspace workspace = null,
            string modelKey = null)
        {
            var symbols = diagram.AllSymbols.ToArray();
            var rectangles = symbols.Where(x => x.Rect != null).Select(x => x.Rect).ToArray();
            var points = symbols.SelectMany(x => x.Points).ToArray();
            if (rectangles.Length == 0 && points.Length == 0)
            {
                writer.WriteLine("<svg xmlns=\"http://www.w3.org/2000/svg\" role=\"img\" aria-label=\"" + H(diagram.Name ?? diagram.Code) + "\" viewBox=\"0 0 1 1\"><title>No positioned symbols in this diagram.</title></svg>");
                return;
            }

            var xs = rectangles.SelectMany(x => new[] { x.X1, x.X2 }).Concat(points.Select(x => x.X)).ToArray();
            var ys = rectangles.SelectMany(x => new[] { x.Y1, x.Y2 }).Concat(points.Select(x => x.Y)).ToArray();
            var minX = xs.Min();
            var maxX = xs.Max();
            var minY = ys.Min();
            var maxY = ys.Max();
            if (options?.Style == PdmDiagramStyle.PowerDesigner)
            {
                var display = PdmTableDisplayPreferences.Read(diagram.DisplayPreferences ?? model.DisplayPreferences);
                foreach (var symbol in symbols.Where(x => x.Rect != null))
                {
                    var pane = NativeTablePaneHeight(model, symbol, display, workspace, modelKey);
                    minY = Math.Min(minY, Math.Min(symbol.Rect.Y1, symbol.Rect.Y2) - pane / 2);
                    maxY = Math.Max(maxY, Math.Max(symbol.Rect.Y1, symbol.Rect.Y2) +
                        pane - pane / 2);
                }
            }
            var pad = options?.Style == PdmDiagramStyle.PowerDesigner ? 0 : 240;
            var width = Math.Max(1, maxX - minX) + pad * 2;
            var height = Math.Max(1, maxY - minY) + pad * 2;
            writer.WriteLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" role=\"img\" aria-label=\"{H(diagram.Name ?? diagram.Code)}\" viewBox=\"0 0 {width} {height}\">");
            writer.WriteLine(options?.Style == PdmDiagramStyle.PowerDesigner
                ? "<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>"
                : "<rect width=\"100%\" height=\"100%\" fill=\"#fbfcfc\"/>");

            if (options?.Style == PdmDiagramStyle.PowerDesigner)
            {
                CheckStyles(symbols, options);
                CheckDisplayPreferences(model, diagram, options, workspace, modelKey);
                WritePowerDesigner(model, diagram, minX, maxY, pad, writer, includeLinks,
                    workspace, modelKey, options);
                writer.WriteLine("</svg>");
                return;
            }

            foreach (var symbol in symbols.Where(x => x.Kind == "ArchitectureAreaSymbol" && x.Rect != null))
                WriteShape(symbol, minX, maxY, pad, writer);
            foreach (var symbol in symbols.Where(x => x.Kind == "ReferenceSymbol"))
                WriteReference(model, diagram, symbol, minX, maxY, pad, writer, includeLinks,
                    workspace, modelKey);
            foreach (var symbol in symbols.Where(x => x.Kind == "NoteLinkSymbol" || x.Kind == "ExtendedDependencySymbol" || x.Kind == "PolylineSymbol"))
                WriteLine(diagram, symbol, minX, maxY, pad, writer);
            foreach (var symbol in symbols.Where(x => x.Kind == "PackageSymbol" && x.Rect != null))
                WriteNode(model, symbol, minX, maxY, pad, writer, includeLinks, workspace, modelKey);
            foreach (var symbol in symbols.Where(x => x.Kind == "TableSymbol" && x.Rect != null))
                WriteNode(model, symbol, minX, maxY, pad, writer, includeLinks, workspace, modelKey);
            foreach (var symbol in symbols.Where(x => x.Kind == "NoteSymbol" || x.Kind == "TextSymbol" || x.Kind == "EllipseSymbol" || x.Kind == "PredefinedSymbol"))
                WriteShape(symbol, minX, maxY, pad, writer);
            writer.WriteLine("</svg>");
        }

        /// <summary>
        /// 标记当前未渲染的原生显示选项。
        /// </summary>
        private static void CheckDisplayPreferences(PdmInfo model, PhysicalDiagramInfo diagram,
            PdmDiagramRenderOptions options, PdmWorkspace workspace, string modelKey)
        {
            var display = PdmTableDisplayPreferences.Read(diagram.DisplayPreferences ?? model.DisplayPreferences);
            var unsupported = new List<string>();
            if (display.UnsupportedColumnFilter != null && options.ColumnFilter == null)
                unsupported.Add("Table.Columns._Filter=" + display.UnsupportedColumnFilter);
            foreach (var value in unsupported)
                if (!options.Diagnostics.Any(x => x.Code == "UNSUPPORTED_DIAGRAM_STYLE" &&
                    x.SourceId == diagram.Id && x.Message.Contains(value)))
                    options.Diagnostics.Add(new PdmDiagnostic { Code = "UNSUPPORTED_DIAGRAM_STYLE",
                        SourceId = diagram.Id, Message = value + " is not rendered in PowerDesigner mode." });
            if (display.ShowForeignKeyConstraintName)
                foreach (var symbol in diagram.AllSymbols.Where(x => x.Kind == "ReferenceSymbol"))
                    if ((model.Lookup == null ||
                        !model.Lookup.TryGetReference(symbol.ObjectId, out var reference) ||
                        string.IsNullOrEmpty(reference.ForeignKeyConstraintName)) &&
                        !options.Diagnostics.Any(x => x.Code == "UNRESOLVED_REFERENCE_LABEL" &&
                            x.SourceId == symbol.Id))
                        options.Diagnostics.Add(new PdmDiagnostic { Code = "UNRESOLVED_REFERENCE_LABEL",
                            SourceId = symbol.Id,
                            Message = "Foreign key constraint name is not stored in the PDM; reference code is shown." });
        }

        /// <summary>
        /// 取得原生表图键清单增加的高度。
        /// </summary>
        private static int NativeKeyPaneHeight(PdmInfo model, DiagramSymbolInfo symbol,
            PdmTableDisplayPreferences display, PdmWorkspace workspace, string modelKey)
        {
            if (!display.ShowKeys || symbol.Kind != "TableSymbol" ||
                IsExternal(symbol, workspace, modelKey) || model.Lookup == null ||
                !model.Lookup.TryGetTable(symbol.ObjectId, out var table)) return 0;
            return table.Keys.Count == 0 ? 0 : 750 + (table.Keys.Count - 1) * 975;
        }

        /// <summary>
        /// 取得原生表图索引清单增加的高度。
        /// </summary>
        private static int NativeIndexPaneHeight(PdmInfo model, DiagramSymbolInfo symbol,
            PdmTableDisplayPreferences display, PdmWorkspace workspace, string modelKey)
        {
            if (!display.ShowIndexes || symbol.Kind != "TableSymbol" ||
                IsExternal(symbol, workspace, modelKey) || model.Lookup == null ||
                !model.Lookup.TryGetTable(symbol.ObjectId, out var table)) return 0;
            var keyPane = NativeKeyPaneHeight(model, symbol, display, workspace, modelKey);
            return (keyPane > 0 ? 1275 : 750) + Math.Max(0, table.Indexes.Count - 1) * 975;
        }

        /// <summary>
        /// 取得触发器清单增加的高度。
        /// </summary>
        private static int NativeTriggerPaneHeight(PdmInfo model, DiagramSymbolInfo symbol,
            PdmTableDisplayPreferences display, PdmWorkspace workspace, string modelKey)
        {
            if (!display.ShowTriggers || symbol.Kind != "TableSymbol" || IsExternal(symbol, workspace, modelKey) ||
                model.Lookup == null || !model.Lookup.TryGetTable(symbol.ObjectId, out var table) ) return 0;
            var preceding = NativeKeyPaneHeight(model, symbol, display, workspace, modelKey) +
                NativeIndexPaneHeight(model, symbol, display, workspace, modelKey);
            return (preceding > 0 ? 1275 : 750) + Math.Max(0, table.Triggers.Count - 1) * 975;
        }

        /// <summary>
        /// 取得原生表图清单区域的总高度。
        /// </summary>
        private static int NativeTablePaneHeight(PdmInfo model, DiagramSymbolInfo symbol,
            PdmTableDisplayPreferences display, PdmWorkspace workspace, string modelKey) =>
            NativeKeyPaneHeight(model, symbol, display, workspace, modelKey) +
            NativeIndexPaneHeight(model, symbol, display, workspace, modelKey) +
            NativeTriggerPaneHeight(model, symbol, display, workspace, modelKey);

        /// <summary>
        /// 检查原生图指定字体并记录不可对照原因。
        /// </summary>
        private static void CheckStyles(DiagramSymbolInfo[] symbols, PdmDiagramRenderOptions options)
        {
            foreach (var symbol in symbols)
            {
                var lineSymbol = symbol.Kind == "ReferenceSymbol" ||
                    symbol.Kind == "NoteLinkSymbol" ||
                    symbol.Kind == "ExtendedDependencySymbol" ||
                    symbol.Kind == "PolylineSymbol";
                var fontList = symbol.FontList ?? (symbol.FontName == null ? null :
                    "STRN 0 " + symbol.FontName);
                foreach (var font in PdmDiagramFontMetrics.Families(fontList)
                    .Concat(symbol.RichTextSegments.Select(x => x.FontFamily).Where(x => !string.IsNullOrEmpty(x)))
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var code = options.FontAvailable == null ? "FONT_AVAILABILITY_UNVERIFIED" :
                        options.FontAvailable(font) ? null : "MISSING_DIAGRAM_FONT";
                    if (code != null && !options.Diagnostics.Any(x => x.Code == code &&
                        x.SourceId == symbol.Id && x.Message.Contains("'" + font + "'")))
                        options.Diagnostics.Add(new PdmDiagnostic { Code = code, SourceId = symbol.Id,
                            Message = "Diagram font '" + font + "' is not confirmed available." });
                }
                if ((!string.IsNullOrEmpty(symbol.LineWidth) && !int.TryParse(symbol.LineWidth, out _)) ||
                    (!string.IsNullOrEmpty(symbol.PenStyle) && symbol.PenStyle != "0") ||
                    (!string.IsNullOrEmpty(symbol.BrushStyle) && symbol.BrushStyle != "6") ||
                    (!string.IsNullOrEmpty(symbol.GradientFillMode) &&
                        symbol.GradientFillMode != "65" && symbol.GradientFillMode != "16") ||
                    (!string.IsNullOrEmpty(symbol.TextStyle) && symbol.TextStyle != "0" &&
                        symbol.TextStyle != "4130") ||
                    (!string.IsNullOrEmpty(symbol.DashStyle) &&
                        !(lineSymbol && (symbol.DashStyle == "2" || symbol.DashStyle == "3" ||
                            symbol.DashStyle == "4" || symbol.DashStyle == "5")) &&
                        (symbol.Kind != "TextSymbol" || symbol.DashStyle != "7")) ||
                    (!string.IsNullOrEmpty(symbol.ArrowStyle) && symbol.ArrowStyle != "0" &&
                        symbol.ArrowStyle != "1" && symbol.ArrowStyle != "2") ||
                    (!string.IsNullOrEmpty(symbol.CornerStyle) && symbol.Kind != "ReferenceSymbol" &&
                        symbol.CornerStyle != "0" && symbol.CornerStyle != "1"))
                    if (!options.Diagnostics.Any(x => x.Code == "UNSUPPORTED_DIAGRAM_STYLE" && x.SourceId == symbol.Id))
                        options.Diagnostics.Add(new PdmDiagnostic { Code = "UNSUPPORTED_DIAGRAM_STYLE",
                            SourceId = symbol.Id, Message = "Diagram style cannot be interpreted." });
            }
        }

        /// <summary>
        /// 利用读取的原生基础样式绘制图元。
        /// </summary>
        private static void WritePowerDesigner(PdmInfo model, PhysicalDiagramInfo diagram,
            int minX, int maxY, int pad, TextWriter writer, bool includeLinks,
            PdmWorkspace workspace, string modelKey, PdmDiagramRenderOptions options)
        {
            var viewport = new PdmDiagramViewport(minX, maxY, pad);
            var preferences = diagram.DisplayPreferences ?? model.DisplayPreferences;
            var display = PdmTableDisplayPreferences.Read(preferences);
            foreach (var symbol in NativeDrawOrder(diagram.Symbols))
            {
                if (symbol.Kind == "ReferenceSymbol" || symbol.Kind == "NoteLinkSymbol" ||
                    symbol.Kind == "ExtendedDependencySymbol" || symbol.Kind == "PolylineSymbol")
                {
                    NativeLine(model, diagram, symbol, viewport, writer, display, workspace, modelKey);
                    continue;
                }
                if (symbol.Rect == null) continue;
                var box = viewport.Box(symbol.Rect);
                var x = box.X;
                var y = box.Y;
                var width = box.Width;
                var originalHeight = box.Height;
                var keyPane = NativeKeyPaneHeight(model, symbol, display, workspace, modelKey);
                var indexPane = NativeIndexPaneHeight(model, symbol, display, workspace, modelKey);
                var triggerPane = NativeTriggerPaneHeight(model, symbol, display, workspace, modelKey);
                var pane = keyPane + indexPane + triggerPane;
                var height = originalHeight + pane;
                y -= pane - pane / 2;
                var titleRole = display.ShowName ? "DISPNAME" : "STRN";
                var fontList = symbol.FontList ?? (symbol.FontName == null ? null :
                    "STRN 0 " + symbol.FontName);
                var font = PdmDiagramFontMetrics.ReadRole(fontList, titleRole);
                var fontSize = font.Item2;
                var stroke = H(symbol.LineColor ?? "#333333");
                var fill = H(symbol.FillColor ?? "#ffffff");
                if (!string.IsNullOrEmpty(symbol.GradientFillMode) && symbol.GradientEndColor != null)
                {
                    var gradientId = HtmlIds.Create("pd-gradient", symbol.Id);
                    var vertical = symbol.GradientFillMode == "16";
                    writer.Write("<defs><linearGradient id=\"" + gradientId + "\" x1=\"0\" y1=\"0\" x2=\"" +
                        (vertical ? "0" : "1") + "\" y2=\"1\"><stop offset=\"5%\" stop-color=\"" +
                        (vertical ? fill : H(symbol.GradientEndColor)) +
                        "\"/><stop offset=\"95%\" stop-color=\"" +
                        (vertical ? H(symbol.GradientEndColor) : fill) + "\"/></linearGradient></defs>");
                    fill = "url(#" + gradientId + ")";
                }
                var radius = symbol.CornerStyle == "1" ? 20 : 0;
                var showShadow = PdmSymbolDisplayPreferences.ShadowEnabled(preferences, symbol.Kind);
                var shadow = string.Empty;
                if (symbol.ShadowColor != null && showShadow == true)
                {
                    var shadowId = HtmlIds.Create("pd-shadow", symbol.Id);
                    writer.Write("<defs><filter id=\"" + shadowId +
                        "\" x=\"-20%\" y=\"-20%\" width=\"150%\" height=\"150%\"><feDropShadow dx=\"24\" dy=\"24\" stdDeviation=\"8\" flood-color=\"" +
                        H(symbol.ShadowColor) + "\" flood-opacity=\".28\"/></filter></defs>");
                    shadow = " filter=\"url(#" + shadowId + ")\"";
                }
                var outline = "<rect x=\"" + x + "\" y=\"" + y + "\" width=\"" + width +
                    "\" height=\"" + height + "\" rx=\"" + radius + "\" fill=\"" + fill +
                    "\" stroke=\"" + stroke + "\" stroke-width=\"" + NativeWidth(symbol) + "\"" + shadow + "/>";
                if (symbol.Kind == "EllipseSymbol")
                    outline = "<ellipse cx=\"" + (x + width / 2) + "\" cy=\"" + (y + height / 2) +
                        "\" rx=\"" + (width / 2) + "\" ry=\"" + (height / 2) + "\" fill=\"" + fill +
                        "\" stroke=\"" + stroke + "\" stroke-width=\"" + NativeWidth(symbol) + "\"" + shadow + "/>";
                if (symbol.Kind == "TextSymbol") outline = string.Empty;
                if (symbol.Kind == "NoteSymbol")
                {
                    var fold = Math.Max(1, Math.Min(width, height) / 6);
                    outline = "<path d=\"M " + x + " " + y + " H " + (x + width - fold) +
                        " L " + (x + width) + " " + (y + fold) + " V " + (y + height) +
                        " H " + x + " Z\" fill=\"" + fill + "\" stroke=\"" + stroke +
                        "\" stroke-width=\"" + NativeWidth(symbol) + "\"" + shadow + "/>" +
                        "<path d=\"M " + (x + width - fold) + " " + y + " V " + (y + fold) +
                        " H " + (x + width) + "\" fill=\"none\" stroke=\"" + stroke +
                        "\" stroke-width=\"" + NativeWidth(symbol) + "\"/>";
                }
                TableInfo table = null;
                var external = IsExternal(symbol, workspace, modelKey);
                var localTable = !external && symbol.Kind == "TableSymbol" && model.Lookup != null &&
                    model.Lookup.TryGetTable(symbol.ObjectId, out table);
                var link = includeLinks && localTable;
                if (link) writer.Write("<a href=\"#" + HtmlIds.Create("table", table.Id) + "\">");
                writer.Write("<g data-symbol-id=\"" + H(symbol.Id) + "\" data-object-id=\"" +
                    H(symbol.ObjectId) + "\" data-bounds=\"" + x + " " + y + " " + width +
                    " " + height + "\">" + outline);
                if (symbol.Kind == "ArchitectureAreaSymbol" && symbol.ObjectId != null)
                {
                    var iconId = HtmlIds.Create("pd-area-icon", symbol.Id);
                    writer.Write("<defs><linearGradient id=\"" + iconId +
                        "\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"><stop offset=\"0%\" stop-color=\"#fffaca\"/><stop offset=\"60%\" stop-color=\"#ffe333\"/><stop offset=\"100%\" stop-color=\"#d9a300\"/></linearGradient></defs>");
                    writer.Write("<rect x=\"" + (x + 300) + "\" y=\"" + (y + 300) +
                        "\" width=\"1200\" height=\"1200\" fill=\"url(#" + iconId +
                        ")\" stroke=\"#ad8500\" stroke-width=\"70\"/>");
                }
                var clipId = HtmlIds.Create("pd-text-clip", symbol.Id);
                writer.Write("<defs><clipPath id=\"" + clipId + "\"><rect x=\"" + x + "\" y=\"" + y +
                    "\" width=\"" + width + "\" height=\"" + height + "\"/></clipPath></defs><g clip-path=\"url(#" + clipId + ")\">");
                if (localTable)
                {
                    var header = PdmTableLayout.HeaderHeight(height, fontSize);
                    var foreignKeyColumns = new HashSet<string>(model.AllReferences
                        .Where(reference => reference.ChildTableId == table.Id &&
                            (workspace == null || reference.ChildTableAddress == null ||
                                reference.ChildTableAddress.ModelKey == modelKey))
                        .SelectMany(reference => reference.Joins)
                        .Where(join => workspace == null || join.ChildColumnAddress == null ||
                            join.ChildColumnAddress.ModelKey == modelKey)
                        .Select(join => join.ChildColumnId));
                    var alternateKeyColumns = new HashSet<string>(table.Keys
                        .Where(key => key.Id != table.PrimaryKeyId)
                        .SelectMany(key => key.ColumnIds));
                    writer.Write("<path d=\"M " + x + " " + (y + header) + " H " + (x + width) +
                        "\" stroke=\"" + stroke + "\" stroke-width=\"" + NativeWidth(symbol) + "\"/>");
                    NativeText(writer, x + width / 2, y + Math.Min(header - 25, fontSize * 21 / 20),
                        display.ShowName ? table.Name ?? table.Code : table.Code ?? table.Name,
                        font.Item1, fontSize, "text-anchor=\"middle\"" +
                        PdmDiagramFontMetrics.ReadRoleAttributes(symbol.FontList, titleRole));
                    var row = 1;
                    var columns = NativeVisibleColumns(table, foreignKeyColumns, display, options, symbol.Id);
                    var nameWidth = columns.Length == 0 ? 0 : columns.Max(column =>
                    {
                        var role = column.PrimaryKey ? "TablePkColumns" :
                            foreignKeyColumns.Contains(column.Id) ? "TableFkColumns" : "Columns";
                        var columnFont = PdmDiagramFontMetrics.ReadRole(symbol.FontList, role);
                        return PdmDiagramFontMetrics.Measure(
                            display.ShowName ? column.Name ?? column.Code : column.Code ?? column.Name,
                            options.MeasureText != null ? PdmDiagramFontMetrics.NativeSize(columnFont.Item2) : columnFont.Item2,
                            columnFont.Item1, false, false, options, symbol.Id);
                    });
                    var typeX = x + 375 + (int)Math.Ceiling(nameWidth) + 750;
                    if (display.ShowColumns) foreach (var column in columns)
                    {
                        var foreignKey = foreignKeyColumns.Contains(column.Id);
                        var columnRole = column.PrimaryKey ? "TablePkColumns" :
                            foreignKey ? "TableFkColumns" : "Columns";
                        var columnFont = PdmDiagramFontMetrics.ReadRole(symbol.FontList, columnRole);
                        var columnStyle = PdmDiagramFontMetrics.ReadRoleAttributes(symbol.FontList, columnRole);
                        if (column.PrimaryKey && !columnStyle.Contains("text-decoration"))
                            columnStyle += " text-decoration=\"underline\"";
                        var baseline = PdmTableLayout.ColumnBaseline(y, header, columnFont.Item2, row);
                        if (baseline > y + originalHeight - 20) break;
                        NativeText(writer, x + 375, baseline,
                            display.ShowName ? column.Name ?? column.Code : column.Code ?? column.Name,
                            columnFont.Item1, columnFont.Item2, columnStyle.TrimStart());
                        if (display.ShowDataType)
                            NativeText(writer, typeX, baseline, column.DataType,
                                columnFont.Item1, columnFont.Item2, columnStyle.TrimStart());
                        if (display.ShowKeyIndicator &&
                            (column.PrimaryKey || foreignKey || alternateKeyColumns.Contains(column.Id)))
                            NativeText(writer, x + width - 350, baseline,
                                column.PrimaryKey ? foreignKey ? "<pk,fk>" : "<pk>" :
                                    alternateKeyColumns.Contains(column.Id) ?
                                        foreignKey ? "<ak,fk>" : "<ak>" : "<fk>",
                                columnFont.Item1, columnFont.Item2,
                                "text-anchor=\"end\"" + columnStyle);
                        row++;
                    }
                    if (keyPane > 0)
                    {
                        var keyFont = PdmDiagramFontMetrics.ReadRole(symbol.FontList, "Keys");
                        writer.Write("<path d=\"M " + x + " " + (y + originalHeight - 500) +
                            " H " + (x + width) + "\" stroke=\"" + stroke +
                            "\" stroke-width=\"" + NativeWidth(symbol) + "\"/>");
                        for (var keyRow = 0; keyRow < table.Keys.Count; keyRow++)
                        {
                            var key = table.Keys[keyRow];
                            var label = key.Name ?? key.Code ?? key.Id;
                            var keyBaseline = y + originalHeight + 250 + keyRow * 975;
                            NativeText(writer, x + 1800, keyBaseline, label,
                                keyFont.Item1, keyFont.Item2,
                                PdmDiagramFontMetrics.ReadRoleAttributes(symbol.FontList, "Keys"));
                            NativeText(writer, x + 1800 +
                                (int)Math.Ceiling(PdmDiagramFontMetrics.Measure(label,
                                    options.MeasureText != null ? PdmDiagramFontMetrics.NativeSize(keyFont.Item2) : keyFont.Item2,
                                    keyFont.Item1, false, false, options, symbol.Id)) + 625, keyBaseline,
                                key.Id == table.PrimaryKeyId ? "<pk>" : "<ak>",
                                keyFont.Item1, keyFont.Item2, string.Empty);
                            NativeKeyIcon(writer, x, keyBaseline);
                        }
                    }
                    if (indexPane > 0)
                    {
                        var divider = y + originalHeight + (keyPane > 0 ? keyPane : -500);
                        writer.Write("<path d=\"M " + x + " " + divider + " H " +
                            (x + width) + "\" stroke=\"" + stroke +
                            "\" stroke-width=\"" + NativeWidth(symbol) + "\"/>");
                        var indexFont = PdmDiagramFontMetrics.ReadRole(symbol.FontList, "Indexes");
                        var indexStyle = PdmDiagramFontMetrics.ReadRoleAttributes(symbol.FontList, "Indexes");
                        for (var indexRow = 0; indexRow < table.Indexes.Count; indexRow++)
                        {
                            var index = table.Indexes[indexRow];
                            var baseline = y + originalHeight + (keyPane > 0 ? keyPane + 750 : 250) +
                                indexRow * 975;
                            NativeText(writer, x + 1800, baseline,
                                index.Name ?? index.Code ?? index.Id,
                                indexFont.Item1, indexFont.Item2, indexStyle);
                            NativeIndexIcon(writer, x, baseline);
                        }
                    }
                    if (triggerPane > 0)
                    {
                        var previousPane = keyPane + indexPane;
                        var divider = y + originalHeight + (previousPane > 0 ? previousPane : -500);
                        writer.Write("<path d=\"M " + x + " " + divider + " H " + (x + width) +
                            "\" stroke=\"" + stroke + "\" stroke-width=\"" + NativeWidth(symbol) + "\"/>");
                        var triggerFont = PdmDiagramFontMetrics.ReadRole(symbol.FontList, "Triggers");
                        for (var triggerRow = 0; triggerRow < table.Triggers.Count; triggerRow++)
                        {
                            var trigger = table.Triggers[triggerRow];
                            var baseline = y + originalHeight + (previousPane > 0 ? previousPane + 750 : 250) + triggerRow * 975;
                            NativeText(writer, x + 1800, baseline, trigger.Name ?? trigger.Code ?? trigger.Id,
                                triggerFont.Item1, triggerFont.Item2,
                                PdmDiagramFontMetrics.ReadRoleAttributes(symbol.FontList, "Triggers"));
                            NativeTriggerIcon(writer, x, baseline);
                        }
                    }
                }
                else
                {
                    var label = ExternalLabel(symbol, workspace, modelKey) ??
                        symbol.Text ?? symbol.ObjectId;
                    if (symbol.Kind == "PackageSymbol" && model.Lookup != null &&
                        model.Lookup.TryGetPackage(symbol.ObjectId, out var package)) label = package.Name ?? package.Code;
                    if (symbol.RawText != null && symbol.RawText.StartsWith(@"{\rtf", StringComparison.Ordinal) &&
                        symbol.RichTextSegments.Count > 0)
                    {
                        var noteText = symbol.Kind == "NoteSymbol";
                        var inset = noteText ? fontSize * 4 / 5 : 50;
                        var firstSize = symbol.RichTextSegments.FirstOrDefault(segment => !string.IsNullOrEmpty(segment.Text))?.FontSize;
                        NativeRichText(writer, x + inset,
                            y + (noteText ? (firstSize.HasValue ? inset + PdmDiagramFontMetrics.NativeSize(firstSize.Value * 100) : fontSize * 16 / 5) : fontSize + 40),
                            font.Item1, noteText ? fontSize * 8 / 5 : fontSize, symbol.RichTextSegments,
                            Math.Max(1, width - inset * 2), options, symbol.Id);
                    }
                    else if (!string.IsNullOrEmpty(label))
                    {
                        var centered = symbol.Kind == "ArchitectureAreaSymbol" ||
                            symbol.Kind == "EllipseSymbol" || symbol.Kind == "TextSymbol";
                        var textX = centered ? x + width / 2 +
                            (symbol.Kind == "ArchitectureAreaSymbol" && symbol.ObjectId != null ? 750 : 0) :
                            x + 50;
                        var textY = symbol.Kind == "ArchitectureAreaSymbol" ? y + fontSize + 100 :
                            centered ? y + height / 2 + fontSize / 3 : y + fontSize + 40;
                        if (label.IndexOf('\n') < 0 && PdmDiagramFontMetrics.Measure(label,
                            PdmDiagramFontMetrics.NativeSize(fontSize), font.Item1, false, false, options, symbol.Id) <= width - 100)
                            NativeText(writer, textX, textY, label, font.Item1, fontSize,
                                centered ? "text-anchor=\"middle\"" : string.Empty);
                        else
                            NativeRichText(writer, x + 50, textY, font.Item1,
                                PdmDiagramFontMetrics.NativeSize(fontSize),
                                new[] { new PdmRichTextSegmentInfo { Text = label,
                                    ParagraphAlignment = centered ? "Center" : "Left" } },
                                Math.Max(1, width - 100), options, symbol.Id);
                    }
                }
                writer.Write("</g></g>");
                if (link) writer.Write("</a>");
                writer.WriteLine();
            }
        }

        /// <summary>
        /// 按显示配置筛选列并保留模型中的顺序。
        /// </summary>
        private static ColumnInfo[] NativeVisibleColumns(TableInfo table, ISet<string> foreignKeys,
            PdmTableDisplayPreferences display, PdmDiagramRenderOptions options, string sourceId)
        {
            IEnumerable<ColumnInfo> columns = table.Columns;
            var keyIds = new HashSet<string>(table.Keys.SelectMany(key => key.ColumnIds));
            var primaryIds = new HashSet<string>(table.Keys.FirstOrDefault(key => key.Id == table.PrimaryKeyId)?.ColumnIds
                ?? new List<string>());
            if (display.PrimaryKeyOnly) columns = columns.Where(column => primaryIds.Contains(column.Id));
            if (display.KeyColumnsOnly) columns = columns.Where(column => keyIds.Contains(column.Id) || foreignKeys.Contains(column.Id));
            if (display.UnsupportedColumnFilter != null && options?.ColumnFilter != null)
                columns = columns.Where(column =>
                {
                    var include = options.ColumnFilter(table, column, display.UnsupportedColumnFilter);
                    if (!include.HasValue && !options.Diagnostics.Any(x => x.Code == "UNSUPPORTED_DIAGRAM_STYLE" && x.SourceId == sourceId))
                        options.Diagnostics.Add(new PdmDiagnostic { Code = "UNSUPPORTED_DIAGRAM_STYLE", SourceId = sourceId,
                            Message = "Column filter could not interpret: " + display.UnsupportedColumnFilter });
                    return include ?? true;
                });
            if (display.ColumnLimit > 0) columns = columns.Take(display.ColumnLimit);
            return columns.ToArray();
        }

        /// <summary>
        /// 绘制原生触发器清单图标。
        /// </summary>
        private static void NativeTriggerIcon(TextWriter writer, int x, int baseline)
        {
            var points = new[] { new[] { 16, -2 }, new[] { 16, 4 }, new[] { 7, 4 }, new[] { 7, -3 },
                new[] { 11, -3 }, new[] { 14, 0 }, new[] { 17, -3 }, new[] { 14, -6 }, new[] { 11, -3 },
                new[] { 7, -3 }, new[] { 7, -5 }, new[] { 13, -5 } };
            writer.Write("<polyline points=\"" + string.Join(" ", points.Select(point =>
                (x + point[0] * 75) + "," + (baseline + point[1] * 75))) +
                "\" fill=\"none\" stroke=\"#000000\" stroke-width=\"70\"/>");
            writer.Write("<polyline points=\"" + (x + 1125) + "," + (baseline - 375) + " " +
                (x + 1200) + "," + (baseline - 375) + " " + (x + 1200) + "," + (baseline - 300) +
                "\" fill=\"none\" stroke=\"#000000\" stroke-width=\"70\"/>");
        }

        /// <summary>
        /// 绘制 PowerDesigner 键清单图标。
        /// </summary>
        private static void NativeKeyIcon(TextWriter writer, int x, int baseline)
        {
            writer.Write("<polyline points=\"" + (x + 525) + "," + baseline + " " +
                (x + 600) + "," + (baseline + 75) + " " +
                (x + 750) + "," + (baseline + 75) + " " +
                (x + 825) + "," + baseline + " " +
                (x + 1125) + "," + baseline + " " +
                (x + 1125) + "," + (baseline + 75) + " " +
                (x + 1275) + "," + (baseline + 75) + " " +
                (x + 1275) + "," + (baseline - 75) + " " +
                (x + 825) + "," + (baseline - 75) + " " +
                (x + 750) + "," + (baseline - 225) + " " +
                (x + 600) + "," + (baseline - 225) + " " +
                (x + 525) + "," + (baseline - 75) + " " +
                (x + 525) + "," + baseline +
                "\" fill=\"none\" stroke=\"#000000\" stroke-width=\"70\"/>");
        }

        /// <summary>
        /// 绘制 PowerDesigner 索引清单图标。
        /// </summary>
        private static void NativeIndexIcon(TextWriter writer, int x, int baseline)
        {
            writer.Write("<polyline points=\"" + (x + 600) + "," + (baseline + 300) + " " +
                (x + 1200) + "," + (baseline + 300) + " " +
                (x + 1200) + "," + (baseline - 450) + " " +
                (x + 600) + "," + (baseline - 450) + " " +
                (x + 600) + "," + (baseline + 300) +
                "\" fill=\"none\" stroke=\"#000000\" stroke-width=\"70\"/>");
            foreach (var offset in new[] { 0, -300 })
                writer.Write("<polyline points=\"" + (x + 750) + "," + (baseline + 150 + offset) + " " +
                    (x + 975) + "," + (baseline + 150 + offset) + " " +
                    (x + 975) + "," + (baseline + offset) + " " +
                    (x + 750) + "," + (baseline + offset) + " " +
                    (x + 750) + "," + (baseline + 150 + offset) +
                    "\" fill=\"none\" stroke=\"#000000\" stroke-width=\"70\"/>");
        }

        /// <summary>
        /// 按原生图元层级和顺序枚举绘制对象。
        /// </summary>
        private static IEnumerable<DiagramSymbolInfo> NativeDrawOrder(
            IEnumerable<DiagramSymbolInfo> siblings, bool nested = false)
        {
            var ordered = nested
                ? siblings.OrderBy(x => x.Kind == "ReferenceSymbol" || x.Kind == "NoteLinkSymbol" ||
                    x.Kind == "ExtendedDependencySymbol" || x.Kind == "PolylineSymbol" ? 0 : 1)
                    .ThenBy(x => x.DrawOrder)
                : siblings.OrderBy(x => x.DrawOrder);
            foreach (var symbol in ordered)
            {
                yield return symbol;
                foreach (var child in NativeDrawOrder(symbol.SubSymbols, true))
                    yield return child;
            }
        }

        /// <summary>
        /// 绘制原生折线及基础箭头。
        /// </summary>
        private static void NativeLine(PdmInfo model, PhysicalDiagramInfo diagram, DiagramSymbolInfo symbol,
            PdmDiagramViewport viewport, TextWriter writer, PdmTableDisplayPreferences display,
            PdmWorkspace workspace, string modelKey)
        {
            var points = symbol.Points;
            if (points.Count < 2)
            {
                var source = diagram.AllSymbols.FirstOrDefault(x => x.Id == symbol.SourceSymbolId)?.Rect;
                var target = diagram.AllSymbols.FirstOrDefault(x => x.Id == symbol.DestinationSymbolId)?.Rect;
                if (source == null || target == null) return;
                points = new List<DiagramPointInfo>
                {
                    new DiagramPointInfo { X = (source.X1 + source.X2) / 2, Y = (source.Y1 + source.Y2) / 2 },
                    new DiagramPointInfo { X = (target.X1 + target.X2) / 2, Y = (target.Y1 + target.Y2) / 2 }
                };
            }
            var projected = points.Select(x => new DiagramPointInfo
                { X = viewport.X(x.X), Y = viewport.Y(x.Y) }).ToList();
            var sourceSymbol = diagram.AllSymbols.FirstOrDefault(x => x.Id == symbol.SourceSymbolId);
            var targetSymbol = diagram.AllSymbols.FirstOrDefault(x => x.Id == symbol.DestinationSymbolId);
            var sourceBox = sourceSymbol?.Rect;
            var targetBox = targetSymbol?.Rect;
            if (sourceBox != null)
            {
                var clip = viewport.Box(sourceBox);
                var pane = NativeTablePaneHeight(model, sourceSymbol, display, workspace, modelKey);
                clip.Y -= pane - pane / 2;
                clip.Height += pane;
                projected[0] = ClipLineEndpoint(projected[0], projected[1], clip);
            }
            if (targetBox != null)
            {
                var clip = viewport.Box(targetBox);
                var pane = NativeTablePaneHeight(model, targetSymbol, display, workspace, modelKey);
                clip.Y -= pane - pane / 2;
                clip.Height += pane;
                projected[projected.Count - 1] = ClipLineEndpoint(projected[projected.Count - 1],
                    projected[projected.Count - 2], clip);
            }
            var values = string.Join(" ", projected.Select(x => x.X + "," + x.Y));
            var dash = symbol.DashStyle == "2" ? " stroke-dasharray=\"1200 600\"" :
                symbol.DashStyle == "3" ? " stroke-dasharray=\"225 225\"" :
                symbol.DashStyle == "4" ? " stroke-dasharray=\"600 300 150 300\"" :
                symbol.DashStyle == "5" ? " stroke-dasharray=\"600 300 150 300 150 300\"" : string.Empty;
            var marker = string.Empty;
            if (symbol.ArrowStyle == "1" || symbol.ArrowStyle == "2")
            {
                var markerId = HtmlIds.Create("pd-arrow", symbol.Id);
                writer.Write("<defs><marker id=\"" + markerId + "\" markerUnits=\"userSpaceOnUse\" markerWidth=\"900\" markerHeight=\"450\" refX=\"900\" refY=\"225\" orient=\"auto\"><path d=\"M 0 0 L 900 225 L 0 450 Z\" fill=\"" + H(symbol.LineColor ?? "#333333") + "\"/></marker></defs>");
                marker = " marker-end=\"url(#" + markerId + ")\"";
            }
            writer.WriteLine("<polyline data-symbol-id=\"" + H(symbol.Id) + "\" points=\"" + values +
                "\" fill=\"none\" stroke=\"" + H(symbol.LineColor ?? "#333333") +
                "\" stroke-width=\"" + NativeWidth(symbol) + "\"" + dash + marker + "/>");
            if (symbol.Kind == "ReferenceSymbol")
            {
                var label = ExternalLabel(symbol, workspace, modelKey);
                if (label == null && model.Lookup != null &&
                    model.Lookup.TryGetReference(symbol.ObjectId, out var reference))
                    label = display.ShowForeignKeyConstraintName &&
                        !string.IsNullOrEmpty(reference.ForeignKeyConstraintName)
                        ? reference.ForeignKeyConstraintName : reference.Code ?? reference.Name;
                if (string.IsNullOrEmpty(label)) return;
                var first = points[0];
                var last = points[points.Count - 1];
                NativeText(writer, viewport.X((first.X + last.X) / 2),
                    viewport.Y((first.Y + last.Y) / 2) - 120,
                    label, PdmDiagramFontMetrics.Read(symbol.FontList).Item1,
                    PdmDiagramFontMetrics.Read(symbol.FontList).Item2, "text-anchor=\"middle\"");
            }
        }

        /// <summary>
        /// 将图形连线端点裁到所连符号边界。
        /// </summary>
        private static DiagramPointInfo ClipLineEndpoint(DiagramPointInfo start,
            DiagramPointInfo toward, PdmDiagramBox box)
        {
            if (start.X < box.X || start.X > box.X + box.Width ||
                start.Y < box.Y || start.Y > box.Y + box.Height ||
                toward.X >= box.X && toward.X <= box.X + box.Width &&
                toward.Y >= box.Y && toward.Y <= box.Y + box.Height)
                return start;
            var dx = toward.X - start.X;
            var dy = toward.Y - start.Y;
            var tx = dx > 0 ? (box.X + box.Width - start.X) / (double)dx :
                dx < 0 ? (box.X - start.X) / (double)dx : double.PositiveInfinity;
            var ty = dy > 0 ? (box.Y + box.Height - start.Y) / (double)dy :
                dy < 0 ? (box.Y - start.Y) / (double)dy : double.PositiveInfinity;
            var distance = Math.Min(tx, ty);
            if (double.IsInfinity(distance) || distance < 0 || distance > 1) return start;
            return new DiagramPointInfo
            {
                X = (int)Math.Round(start.X + dx * distance),
                Y = (int)Math.Round(start.Y + dy * distance)
            };
        }

        /// <summary>
        /// 写入 SVG 文本节点。
        /// </summary>
        private static void NativeText(TextWriter writer, int x, int y, string value,
            string family, int size, string attributes) =>
            writer.Write("<text x=\"" + x + "\" y=\"" + y + "\" font-family=\"" + H(family) +
                "\" font-size=\"" + PdmDiagramFontMetrics.NativeSize(size) + "\" " +
                attributes + ">" + H(value) + "</text>");

        /// <summary>
        /// 按行布局写入 RTF 样式分段。
        /// </summary>
        private static void NativeRichText(TextWriter writer, int x, int y, string family, int size,
            IEnumerable<PdmRichTextSegmentInfo> segments, int width, PdmDiagramRenderOptions options, string sourceId)
        {
            var lineIndex = 0;
            foreach (var line in PdmRichTextLayout.Layout(segments, family, size, width, options, sourceId))
            {
                var spaces = line.Glyphs.Count(glyph => string.IsNullOrWhiteSpace(glyph.Text));
                var justify = line.Alignment == "Justify" && !line.ParagraphEnd && spaces > 0;
                var extra = justify ? Math.Max(0, width - line.Width) / spaces : 0;
                var position = x + (line.Alignment == "Center" ? Math.Max(0, width - line.Width) / 2 :
                    line.Alignment == "Right" ? Math.Max(0, width - line.Width) : 0);
                writer.Write("<text xml:space=\"preserve\" data-line-index=\"" + lineIndex++ + "\" y=\"" + y +
                    "\" font-family=\"" + H(family) + "\" font-size=\"" + size + "\">");
                var index = 0;
                while (index < line.Glyphs.Count)
                {
                    var first = line.Glyphs[index];
                    var glyphs = new List<PdmRichTextLayout.Glyph> { first };
                    while (++index < line.Glyphs.Count && ReferenceEquals(line.Glyphs[index].Style, first.Style) &&
                        (!justify || (!string.IsNullOrWhiteSpace(first.Text) && !string.IsNullOrWhiteSpace(line.Glyphs[index].Text))))
                        glyphs.Add(line.Glyphs[index]);
                    var segment = first.Style;
                    writer.Write("<tspan x=\"" + position.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                        "\" font-weight=\"" + (segment.Bold ? "bold" : "normal") +
                        "\" font-style=\"" + (segment.Italic ? "italic" : "normal") +
                        "\" font-size=\"" + first.Size + "\" font-family=\"" + H(first.Family) + "\"" +
                        (segment.ForegroundColor == null ? string.Empty : " fill=\"" + H(segment.ForegroundColor) + "\"") +
                        (segment.Underline ? " text-decoration=\"underline\"" : " text-decoration=\"none\"") +
                        ">" + H(string.Concat(glyphs.Select(glyph => glyph.Text))) + "</tspan>");
                    position += glyphs.Sum(glyph => glyph.Width) + extra * glyphs.Count(glyph => string.IsNullOrWhiteSpace(glyph.Text));
                }
                writer.Write("</text>");
                y += line.Height;
            }
        }

        /// <summary>
        /// 取得正数线宽。
        /// </summary>
        private static int NativeWidth(DiagramSymbolInfo symbol) =>
            int.TryParse(symbol.LineWidth, out var width) && width > 0 ? width : 70;

        /// <summary>
        /// 写入表或包节点。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="symbol">表或包图形符号。</param>
        /// <param name="minX">图形区域的最小横坐标。</param>
        /// <param name="maxY">图形区域的最大纵坐标。</param>
        /// <param name="pad">SVG 边缘留白。</param>
        /// <param name="writer">SVG 输出。</param>
        /// <param name="includeLinks">是否为表节点生成链接。</param>
        private static void WriteNode(PdmInfo model, DiagramSymbolInfo symbol, int minX, int maxY,
            int pad, TextWriter writer, bool includeLinks, PdmWorkspace workspace, string modelKey)
        {
            var rect = symbol.Rect;
            var x = Math.Min(rect.X1, rect.X2) - minX + pad;
            var y = maxY - Math.Max(rect.Y1, rect.Y2) + pad;
            var width = Math.Max(1, Math.Abs(rect.X2 - rect.X1));
            var height = Math.Max(1, Math.Abs(rect.Y2 - rect.Y1));
            var package = symbol.Kind == "PackageSymbol";
            var fill = symbol.FillColor ?? (package ? "#e7f0ec" : "#fff");
            var stroke = symbol.LineColor ?? (package ? "#5e8372" : "#246d75");
            PackageInfo packageInfo = null;
            TableInfo tableInfo = null;
            var external = IsExternal(symbol, workspace, modelKey);
            var label = external ? ExternalLabel(symbol, workspace, modelKey) : package
                ? model.Lookup != null && model.Lookup.TryGetPackage(symbol.ObjectId, out packageInfo) ? packageInfo.Name : null
                : model.Lookup != null && model.Lookup.TryGetTable(symbol.ObjectId, out tableInfo) ? tableInfo.Code : null;
            var fontSize = Math.Max(60, Math.Min(165, width / 11));
            var fullLabel = label ?? symbol.ObjectId ?? string.Empty;
            var maxChars = Math.Max(1, (int)((width - 130) / (fontSize * 0.65)));
            var shownLabel = fullLabel.Length > maxChars ? fullLabel.Substring(0, Math.Max(1, maxChars - 1)) + "..." : fullLabel;
            var canLink = includeLinks && !package && !external && tableInfo != null;
            var linkStart = canLink ? "<a href=\"#" + HtmlIds.Create("table", symbol.ObjectId) + "\">" : string.Empty;
            var linkEnd = canLink ? "</a>" : string.Empty;
            writer.WriteLine(linkStart + $"<g data-symbol-id=\"{H(symbol.Id)}\" data-object-id=\"{H(symbol.ObjectId)}\"><title>{H(fullLabel)}</title><rect x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{height}\" rx=\"24\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"18\"/><text x=\"{x + 65}\" y=\"{y + fontSize + 50}\" font-size=\"{fontSize}\" font-family=\"Arial,sans-serif\" fill=\"#24343a\">{H(shownLabel)}</text></g>" + linkEnd);
        }

        /// <summary>
        /// 写入引用连线。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="diagram">所属物理图。</param>
        /// <param name="symbol">引用图形符号。</param>
        /// <param name="minX">图形区域的最小横坐标。</param>
        /// <param name="maxY">图形区域的最大纵坐标。</param>
        /// <param name="pad">SVG 边缘留白。</param>
        /// <param name="writer">SVG 输出。</param>
        /// <param name="includeLinks">是否为连线生成链接。</param>
        private static void WriteReference(PdmInfo model, PhysicalDiagramInfo diagram, DiagramSymbolInfo symbol,
            int minX, int maxY, int pad, TextWriter writer, bool includeLinks,
            PdmWorkspace workspace, string modelKey)
        {
            var points = symbol.Points;
            if (points.Count < 2)
            {
                var source = diagram.AllSymbols.FirstOrDefault(x => x.Id == symbol.SourceSymbolId)?.Rect;
                var destination = diagram.AllSymbols.FirstOrDefault(x => x.Id == symbol.DestinationSymbolId)?.Rect;
                if (source == null || destination == null) return;
                points = new List<DiagramPointInfo>
                {
                    new DiagramPointInfo { X = (source.X1 + source.X2) / 2, Y = (source.Y1 + source.Y2) / 2 },
                    new DiagramPointInfo { X = (destination.X1 + destination.X2) / 2, Y = (destination.Y1 + destination.Y2) / 2 }
                };
            }
            var svgPoints = string.Join(" ", points.Select(x => (x.X - minX + pad) + "," + (maxY - x.Y + pad)));
            ReferenceInfo reference;
            var external = IsExternal(symbol, workspace, modelKey);
            var label = external ? ExternalLabel(symbol, workspace, modelKey) :
                model.Lookup != null && model.Lookup.TryGetReference(symbol.ObjectId, out reference) ? reference.Code : symbol.ObjectId;
            var linkStart = includeLinks && !external ? "<a href=\"#" + HtmlIds.Create("reference", symbol.ObjectId) + "\">" : string.Empty;
            var linkEnd = includeLinks && !external ? "</a>" : string.Empty;
            writer.WriteLine(linkStart + $"<g data-symbol-id=\"{H(symbol.Id)}\" data-object-id=\"{H(symbol.ObjectId)}\"><title>{H(label)}</title><polyline points=\"{svgPoints}\" fill=\"none\" stroke=\"{H(symbol.LineColor ?? "#bb6b49")}\" stroke-width=\"24\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></g>" + linkEnd);
        }

        /// <summary>
        /// 判断图元是否指向工作区中的外部模型。
        /// </summary>
        private static bool IsExternal(DiagramSymbolInfo symbol, PdmWorkspace workspace, string modelKey) =>
            workspace != null && symbol.ObjectAddress != null &&
            symbol.ObjectAddress.ModelKey != modelKey;

        /// <summary>
        /// 取得外部对象在物理图中的可读名称。
        /// </summary>
        private static string ExternalLabel(DiagramSymbolInfo symbol, PdmWorkspace workspace, string modelKey)
        {
            if (!IsExternal(symbol, workspace, modelKey)) return null;
            var address = symbol.ObjectAddress;
            if (workspace.TryGetModel(address.ModelKey, out var target))
            {
                if (symbol.Kind == "TableSymbol" &&
                    target.Lookup.TryGetTable(address.PdmId, out var table))
                    return address.ModelKey + ":" + (table.Code ?? table.Name ?? table.Id);
                if (symbol.Kind == "PackageSymbol" &&
                    target.Lookup.TryGetPackage(address.PdmId, out var package))
                    return address.ModelKey + ":" + (package.Code ?? package.Name ?? package.Id);
                if (symbol.Kind == "ReferenceSymbol" &&
                    target.Lookup.TryGetReference(address.PdmId, out var reference))
                    return address.ModelKey + ":" + (reference.Code ?? reference.Name ?? reference.Id);
            }
            return address.ModelKey + ":" + address.PdmId;
        }

        /// <summary>
        /// 写入普通折线。
        /// </summary>
        /// <param name="diagram">所属物理图。</param>
        /// <param name="symbol">折线图形符号。</param>
        /// <param name="minX">图形区域的最小横坐标。</param>
        /// <param name="maxY">图形区域的最大纵坐标。</param>
        /// <param name="pad">SVG 边缘留白。</param>
        /// <param name="writer">SVG 输出。</param>
        private static void WriteLine(PhysicalDiagramInfo diagram, DiagramSymbolInfo symbol, int minX, int maxY, int pad, TextWriter writer)
        {
            var points = symbol.Points;
            if (points.Count < 2)
            {
                var source = diagram.AllSymbols.FirstOrDefault(x => x.Id == symbol.SourceSymbolId)?.Rect;
                var destination = diagram.AllSymbols.FirstOrDefault(x => x.Id == symbol.DestinationSymbolId)?.Rect;
                if (source == null || destination == null) return;
                points = new List<DiagramPointInfo>
                {
                    new DiagramPointInfo { X = (source.X1 + source.X2) / 2, Y = (source.Y1 + source.Y2) / 2 },
                    new DiagramPointInfo { X = (destination.X1 + destination.X2) / 2, Y = (destination.Y1 + destination.Y2) / 2 }
                };
            }
            var svgPoints = string.Join(" ", points.Select(x => (x.X - minX + pad) + "," + (maxY - x.Y + pad)));
            writer.WriteLine($"<g data-symbol-id=\"{H(symbol.Id)}\"><polyline points=\"{svgPoints}\" fill=\"none\" stroke=\"{H(symbol.LineColor ?? "#64717a")}\" stroke-width=\"18\"/></g>");
        }

        /// <summary>
        /// 写入注释或形状符号。
        /// </summary>
        /// <param name="symbol">待写入的注释或形状符号。</param>
        /// <param name="minX">图形区域的最小横坐标。</param>
        /// <param name="maxY">图形区域的最大纵坐标。</param>
        /// <param name="pad">SVG 边缘留白。</param>
        /// <param name="writer">SVG 输出。</param>
        private static void WriteShape(DiagramSymbolInfo symbol, int minX, int maxY, int pad, TextWriter writer)
        {
            var rect = symbol.Rect;
            if (rect == null) return;
            var x = Math.Min(rect.X1, rect.X2) - minX + pad;
            var y = maxY - Math.Max(rect.Y1, rect.Y2) + pad;
            var width = Math.Max(1, Math.Abs(rect.X2 - rect.X1));
            var height = Math.Max(1, Math.Abs(rect.Y2 - rect.Y1));
            var fill = H(symbol.FillColor ?? (symbol.Kind == "TextSymbol" ? "none" : "#ffffff"));
            var stroke = H(symbol.LineColor ?? "#67747b");
            var outline = symbol.Kind == "EllipseSymbol"
                ? $"<ellipse cx=\"{x + width / 2}\" cy=\"{y + height / 2}\" rx=\"{width / 2}\" ry=\"{height / 2}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"14\"/>"
                : $"<rect x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{height}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"14\"/>";
            writer.Write($"<g data-symbol-id=\"{H(symbol.Id)}\" data-symbol-type=\"{H(symbol.SymbolType)}\">{outline}");
            if (!string.IsNullOrEmpty(symbol.Text))
            {
                var fontSize = Math.Max(60, Math.Min(160, height / Math.Max(2, symbol.Text.Split('\n').Length + 1)));
                var line = 0;
                foreach (var text in symbol.Text.Split('\n'))
                    writer.Write($"<text x=\"{x + 55}\" y=\"{y + 60 + fontSize * ++line}\" font-size=\"{fontSize}\" font-family=\"Arial,sans-serif\" fill=\"#26343a\">{H(text)}</text>");
            }
            writer.WriteLine("</g>");
        }

        /// <summary>
        /// 转义 SVG 中的文本。
        /// </summary>
        /// <param name="value">待转义文本。</param>
        /// <returns>HTML/XML 转义后的文本；输入为 <see langword="null"/> 时返回空字符串。</returns>
        private static string H(string value) => WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
