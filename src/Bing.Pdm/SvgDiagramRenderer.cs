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
            const int pad = 240;
            var width = Math.Max(1, maxX - minX) + pad * 2;
            var height = Math.Max(1, maxY - minY) + pad * 2;
            writer.WriteLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" role=\"img\" aria-label=\"{H(diagram.Name ?? diagram.Code)}\" viewBox=\"0 0 {width} {height}\">");
            writer.WriteLine("<rect width=\"100%\" height=\"100%\" fill=\"#fbfcfc\"/>");

            foreach (var symbol in symbols.Where(x => x.Kind == "ArchitectureAreaSymbol" && x.Rect != null))
                WriteShape(symbol, minX, maxY, pad, writer);
            foreach (var symbol in symbols.Where(x => x.Kind == "ReferenceSymbol"))
                WriteReference(model, diagram, symbol, minX, maxY, pad, writer, includeLinks);
            foreach (var symbol in symbols.Where(x => x.Kind == "NoteLinkSymbol" || x.Kind == "ExtendedDependencySymbol" || x.Kind == "PolylineSymbol"))
                WriteLine(diagram, symbol, minX, maxY, pad, writer);
            foreach (var symbol in symbols.Where(x => x.Kind == "PackageSymbol" && x.Rect != null))
                WriteNode(model, symbol, minX, maxY, pad, writer, includeLinks);
            foreach (var symbol in symbols.Where(x => x.Kind == "TableSymbol" && x.Rect != null))
                WriteNode(model, symbol, minX, maxY, pad, writer, includeLinks);
            foreach (var symbol in symbols.Where(x => x.Kind == "NoteSymbol" || x.Kind == "TextSymbol" || x.Kind == "EllipseSymbol" || x.Kind == "PredefinedSymbol"))
                WriteShape(symbol, minX, maxY, pad, writer);
            writer.WriteLine("</svg>");
        }

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
        private static void WriteNode(PdmInfo model, DiagramSymbolInfo symbol, int minX, int maxY, int pad, TextWriter writer, bool includeLinks)
        {
            var rect = symbol.Rect;
            var x = Math.Min(rect.X1, rect.X2) - minX + pad;
            var y = maxY - Math.Max(rect.Y1, rect.Y2) + pad;
            var width = Math.Max(1, Math.Abs(rect.X2 - rect.X1));
            var height = Math.Max(1, Math.Abs(rect.Y2 - rect.Y1));
            var package = symbol.Kind == "PackageSymbol";
            var fill = symbol.FillColor ?? (package ? "#e7f0ec" : "#fff");
            var stroke = symbol.LineColor ?? (package ? "#5e8372" : "#246d75");
            PackageInfo packageInfo;
            TableInfo tableInfo;
            var label = package
                ? model.Lookup != null && model.Lookup.TryGetPackage(symbol.ObjectId, out packageInfo) ? packageInfo.Name : null
                : model.Lookup != null && model.Lookup.TryGetTable(symbol.ObjectId, out tableInfo) ? tableInfo.Code : null;
            var fontSize = Math.Max(60, Math.Min(165, width / 11));
            var fullLabel = label ?? symbol.ObjectId ?? string.Empty;
            var maxChars = Math.Max(1, (int)((width - 130) / (fontSize * 0.65)));
            var shownLabel = fullLabel.Length > maxChars ? fullLabel.Substring(0, Math.Max(1, maxChars - 1)) + "..." : fullLabel;
            var linkStart = includeLinks && !package ? "<a href=\"#" + HtmlIds.Create("table", symbol.ObjectId) + "\">" : string.Empty;
            var linkEnd = includeLinks && !package ? "</a>" : string.Empty;
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
        private static void WriteReference(PdmInfo model, PhysicalDiagramInfo diagram, DiagramSymbolInfo symbol, int minX, int maxY, int pad, TextWriter writer, bool includeLinks)
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
            var label = model.Lookup != null && model.Lookup.TryGetReference(symbol.ObjectId, out reference) ? reference.Code : symbol.ObjectId;
            var linkStart = includeLinks ? "<a href=\"#" + HtmlIds.Create("reference", symbol.ObjectId) + "\">" : string.Empty;
            var linkEnd = includeLinks ? "</a>" : string.Empty;
            writer.WriteLine(linkStart + $"<g data-symbol-id=\"{H(symbol.Id)}\" data-object-id=\"{H(symbol.ObjectId)}\"><title>{H(label)}</title><polyline points=\"{svgPoints}\" fill=\"none\" stroke=\"{H(symbol.LineColor ?? "#bb6b49")}\" stroke-width=\"24\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></g>" + linkEnd);
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
