using System;
using System.Linq;
using Bing.Pdm.Models.PhysicalDiagrams;

namespace Bing.Pdm
{
    /// <summary>
    /// 将 PDM 坐标映射到 SVG 画布。
    /// </summary>
    internal sealed class PdmDiagramViewport
    {
        /// <summary>
        /// 获取原始坐标的最小横值。
        /// </summary>
        private readonly int _minX;
        /// <summary>
        /// 获取原始坐标的最大纵值。
        /// </summary>
        private readonly int _maxY;
        /// <summary>
        /// 获取画布留白。
        /// </summary>
        private readonly int _pad;

        /// <summary>
        /// 初始化一个 <see cref="PdmDiagramViewport"/> 类型的实例。
        /// </summary>
        /// <param name="minX">原始坐标的最小横值。</param>
        /// <param name="maxY">原始坐标的最大纵值。</param>
        /// <param name="pad">画布边缘留白。</param>
        public PdmDiagramViewport(int minX, int maxY, int pad)
        {
            _minX = minX; _maxY = maxY; _pad = pad;
        }

        /// <summary>
        /// 转换横坐标。
        /// </summary>
        /// <param name="value">原始横坐标。</param>
        /// <returns>映射后的画布横坐标。</returns>
        public int X(int value) => value - _minX + _pad;

        /// <summary>
        /// 转换纵坐标。
        /// </summary>
        /// <param name="value">原始纵坐标。</param>
        /// <returns>映射后的画布纵坐标。</returns>
        public int Y(int value) => _maxY - value + _pad;

        /// <summary>
        /// 转换矩形左上角及尺寸。
        /// </summary>
        /// <param name="rect">原始矩形范围。</param>
        /// <returns>映射后的画布矩形。</returns>
        public PdmDiagramBox Box(DiagramRectangleInfo rect) => new PdmDiagramBox
        {
            X = X(Math.Min(rect.X1, rect.X2)),
            Y = Y(Math.Max(rect.Y1, rect.Y2)),
            Width = Math.Max(1, Math.Abs(rect.X2 - rect.X1)),
            Height = Math.Max(1, Math.Abs(rect.Y2 - rect.Y1))
        };
    }

    /// <summary>
    /// SVG 画布中的矩形。
    /// </summary>
    internal sealed class PdmDiagramBox
    {
        /// <summary>
        /// 获取或设置横坐标。
        /// </summary>
        public int X { get; set; }
        /// <summary>
        /// 获取或设置纵坐标。
        /// </summary>
        public int Y { get; set; }
        /// <summary>
        /// 获取或设置宽度。
        /// </summary>
        public int Width { get; set; }
        /// <summary>
        /// 获取或设置高度。
        /// </summary>
        public int Height { get; set; }
    }

    /// <summary>
    /// 解析 PowerDesigner 的字体度量。
    /// </summary>
    internal static class PdmDiagramFontMetrics
    {
        /// <summary>
        /// 读取 FontList 首行的字体和字号。
        /// </summary>
        /// <param name="list">PowerDesigner 的 FontList 文本。</param>
        /// <returns>字体名称和图形字号。</returns>
        public static Tuple<string, int> Read(string list) => ReadRole(list, null);

        /// <summary>
        /// 列出 FontList 中声明的字体。
        /// </summary>
        /// <param name="list">PowerDesigner 的 FontList 文本。</param>
        /// <returns>去重后的字体名称。</returns>
        public static string[] Families(string list) =>
            (list ?? string.Empty).Split('\n')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => ReadRole(x, null).Item1)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        /// <summary>
        /// 读取指定文字角色的字体和字号。
        /// </summary>
        /// <param name="list">PowerDesigner 的 FontList 文本。</param>
        /// <param name="role">文字角色；为空时读取首行。</param>
        /// <returns>字体名称和图形字号。</returns>
        public static Tuple<string, int> ReadRole(string list, string role)
        {
            var first = RoleLine(list, role);
            var start = first.IndexOf(' ');
            if (start >= 0) start = first.IndexOf(' ', start + 1);
            var family = start >= 0 ? first.Substring(start + 1).Split(',')[0].Trim() : "Arial";
            var pieces = first.Split(',');
            var points = pieces.Length > 1 && int.TryParse(pieces[1], out var value) ? value : 8;
            return Tuple.Create(string.IsNullOrEmpty(family) ? "Arial" : family, Math.Max(50, points * 100));
        }

        /// <summary>
        /// 读取指定文字角色的 SVG 字形属性。
        /// </summary>
        /// <param name="list">PowerDesigner 的 FontList 文本。</param>
        /// <param name="role">文字角色；为空时读取首行。</param>
        /// <returns>可直接写入 SVG 的字形属性文本。</returns>
        public static string ReadRoleAttributes(string list, string role)
        {
            var parts = RoleLine(list, role).Split(',');
            var style = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            var attributes = string.Empty;
            if (style.IndexOf('B') >= 0) attributes += " font-weight=\"bold\"";
            if (style.IndexOf('I') >= 0) attributes += " font-style=\"italic\"";
            if (style.IndexOf('U') >= 0) attributes += " text-decoration=\"underline\"";
            return attributes;
        }

        /// <summary>
        /// 将小字号换算为原生 SVG 的图形字号。
        /// </summary>
        /// <remarks>16.7.4 原生输出在 96 DPI 下将 8pt 写为 10px；每像素约为 75 个图形单位。</remarks>
        /// <param name="size">PowerDesigner 图形字号。</param>
        /// <returns>换算后的原生图形字号。</returns>
        public static int NativeSize(int size) => size <= 800 ? Math.Max(50, size / 75 * 75) : size;

        /// <summary>
        /// 测量图形文本宽度。
        /// </summary>
        /// <param name="text">待测文本。</param>
        /// <param name="fontSize">图形字号。</param>
        /// <param name="fontFamily">字体名称。</param>
        /// <param name="bold">是否使用粗体。</param>
        /// <param name="italic">是否使用斜体。</param>
        /// <param name="options">图形渲染选项。</param>
        /// <param name="sourceId">产生诊断时使用的源对象标识。</param>
        /// <returns>文本宽度。</returns>
        public static double Measure(string text, int fontSize, string fontFamily, bool bold, bool italic,
            PdmDiagramRenderOptions options, string sourceId)
        {
            if (options?.MeasureText != null)
            {
                var width = options.MeasureText(new PdmDiagramTextMeasureInfo
                {
                    Text = text ?? string.Empty,
                    FontFamily = fontFamily,
                    FontSize = fontSize,
                    Bold = bold,
                    Italic = italic
                });
                if (!double.IsNaN(width) && !double.IsInfinity(width) && width >= 0) return width;
            }
            if (options != null && !options.Diagnostics.Any(x => x.Code == "APPROXIMATE_FONT_METRICS" && x.SourceId == sourceId))
                options.Diagnostics.Add(new Bing.Pdm.Models.PdmDiagnostic
                {
                    Code = "APPROXIMATE_FONT_METRICS",
                    SourceId = sourceId,
                    Message = "A font measurement provider is unavailable; text widths are estimated."
                });
            return EstimateColumnWidth(text, fontSize, fontFamily);
        }

        /// <summary>
        /// 估算表字段文本在图形坐标中的宽度。
        /// </summary>
        /// <param name="text">待测文本。</param>
        /// <param name="fontSize">图形字号。</param>
        /// <param name="fontFamily">字体名称。</param>
        /// <returns>估算的文本宽度。</returns>
        public static int EstimateColumnWidth(string text, int fontSize, string fontFamily)
        {
            var glyphUnits = (text ?? string.Empty).Sum(x => GlyphWidth(x));
            var scale = string.Equals(fontFamily, "Times New Roman",
                StringComparison.OrdinalIgnoreCase) ? 0.84 : 0.93;
            return (int)Math.Round(glyphUnits * fontSize * scale / 1000.0);
        }

        /// <summary>
        /// 获取常用拉丁字形的 Arial 宽度，其他文字按全角估算。
        /// </summary>
        private static int GlyphWidth(char value)
        {
            if (value >= '0' && value <= '9') return 556;
            switch (value)
            {
                case ' ':
                case 'I':
                case 'f':
                case 't':
                case '(':
                case ')':
                    return value == '(' || value == ')' ? 333 : 278;
                case 'i': case 'j': case 'l': return 222;
                case 'r': case '-': return 333;
                case 'c':
                case 'k':
                case 's':
                case 'v':
                case 'x':
                case 'y':
                case 'z':
                    return 500;
                case 'a':
                case 'b':
                case 'd':
                case 'e':
                case 'g':
                case 'h':
                case 'n':
                case 'o':
                case 'p':
                case 'q':
                case 'u': return 556;
                case 'm': case 'M': return 833;
                case 'w': return 722;
                case 'C': case 'D': case 'H': case 'N': case 'R': case 'U': return 722;
                case 'A':
                case 'B':
                case 'E':
                case 'K':
                case 'P':
                case 'S':
                case 'V':
                case 'X':
                case 'Y': return 667;
                case 'F': case 'T': case 'Z': return 611;
                case 'G': case 'O': case 'Q': return 778;
                case 'J': return 500;
                case 'L': return 556;
                case 'W': return 944;
                default: return value > 127 ? 1000 : 556;
            }
        }

        /// <summary>
        /// 查找指定文字角色的字体配置行。
        /// </summary>
        private static string RoleLine(string list, string role)
        {
            var lines = (list ?? string.Empty).Split('\n');
            return (role == null ? lines.FirstOrDefault() :
                lines.FirstOrDefault(x => x.TrimStart().StartsWith(role + " ", StringComparison.OrdinalIgnoreCase))
                ?? lines.FirstOrDefault()) ?? string.Empty;
        }
    }

    /// <summary>
    /// 读取物理图的符号显示配置。
    /// </summary>
    internal static class PdmSymbolDisplayPreferences
    {
        /// <summary>
        /// 获取指定符号的阴影开关；缺失时返回空值。
        /// </summary>
        /// <param name="text">符号显示配置文本。</param>
        /// <param name="kind">符号类型。</param>
        /// <returns>明确禁用时返回 <see langword="false"/>，启用时返回 <see langword="true"/>，无法判断时返回 <see langword="null"/>。</returns>
        public static bool? ShadowEnabled(string text, string kind)
        {
            var symbolType = kind == "TableSymbol" ? "TABL" :
                kind == "ReferenceSymbol" ? "REFR" :
                kind == "PackageSymbol" ? "PDMPCKG" : null;
            if (symbolType == null || string.IsNullOrEmpty(text)) return null;
            var section = @"[DisplayPreferences\Symbol\" + symbolType + "]";
            var inSection = false;
            foreach (var raw in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var line = raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    inSection = line == section;
                    continue;
                }
                if (!inSection || !line.StartsWith("Shadow=", StringComparison.Ordinal)) continue;
                var value = line.Substring("Shadow=".Length).Trim();
                if (value == "0") return false;
                if (value == "-1" || value == "1") return true;
                return null;
            }
            return null;
        }
    }

    /// <summary>
    /// 读取模型级表图形显示选项。
    /// </summary>
    internal sealed class PdmTableDisplayPreferences
    {
        /// <summary>
        /// 获取表标题是否显示名称。
        /// </summary>
        public bool ShowName { get; private set; } = true;
        /// <summary>
        /// 获取列清单是否可见。
        /// </summary>
        public bool ShowColumns { get; private set; } = true;
        /// <summary>
        /// 获取列数据类型是否可见。
        /// </summary>
        public bool ShowDataType { get; private set; } = true;
        /// <summary>
        /// 获取列键标记是否可见。
        /// </summary>
        public bool ShowKeyIndicator { get; private set; } = true;
        /// <summary>
        /// 获取列的显示上限，零表示不限。
        /// </summary>
        public int ColumnLimit { get; private set; }
        /// <summary>
        /// 获取是否只显示主键列。
        /// </summary>
        public bool PrimaryKeyOnly { get; private set; }
        /// <summary>
        /// 获取是否只显示键列。
        /// </summary>
        public bool KeyColumnsOnly { get; private set; }
        /// <summary>
        /// 获取是否请求显示键清单。
        /// </summary>
        public bool ShowKeys { get; private set; }
        /// <summary>
        /// 获取是否请求显示索引清单。
        /// </summary>
        public bool ShowIndexes { get; private set; }
        /// <summary>
        /// 获取是否请求显示触发器清单。
        /// </summary>
        public bool ShowTriggers { get; private set; }
        /// <summary>
        /// 获取关系标签是否显示外键约束名称。
        /// </summary>
        public bool ShowForeignKeyConstraintName { get; private set; }
        /// <summary>
        /// 获取无法解释的字段过滤器。
        /// </summary>
        public string UnsupportedColumnFilter { get; private set; }

        /// <summary>
        /// 读取 PowerDesigner 对象显示配置。
        /// </summary>
        /// <param name="text">对象显示配置文本。</param>
        /// <returns>解析得到的表显示选项。</returns>
        public static PdmTableDisplayPreferences Read(string text)
        {
            var result = new PdmTableDisplayPreferences();
            if (string.IsNullOrEmpty(text)) return result;
            var inObjectSection = false;
            foreach (var raw in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var line = raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    inObjectSection = line == @"[DisplayPreferences\Object]";
                    continue;
                }
                if (!inObjectSection) continue;
                var separator = line.IndexOf('=');
                if (separator < 0) continue;
                var key = line.Substring(0, separator).Trim();
                var value = line.Substring(separator + 1).Trim();
                switch (key)
                {
                    case "Table.DisplayName": result.ShowName = value == "Yes"; break;
                    case "Table.Columns": result.ShowColumns = value == "Yes"; break;
                    case "Table.Columns._Columns":
                        result.ShowDataType = value.Split(' ').Contains("DataType");
                        result.ShowKeyIndicator = value.Split(' ').Contains("KeyIndicator");
                        break;
                    case "Table.Columns._Limit":
                        if (int.TryParse(value, out var limit) && limit > 0) result.ColumnLimit = limit;
                        break;
                    case "Table.Columns._Filter":
                        result.PrimaryKeyOnly = value.Split(' ').Contains("PDMCOLNPK");
                        result.KeyColumnsOnly = value.Split(' ').Contains("PDMCOLNKEY");
                        if (!result.PrimaryKeyOnly && !result.KeyColumnsOnly && !value.Split(' ').Contains("PDMCOLNALL"))
                            result.UnsupportedColumnFilter = value;
                        break;
                    case "Table.Keys": result.ShowKeys = value == "Yes"; break;
                    case "Table.Indexes": result.ShowIndexes = value == "Yes"; break;
                    case "Table.Triggers": result.ShowTriggers = value == "Yes"; break;
                    case "Reference.ForeignKeyConstraintName":
                        result.ShowForeignKeyConstraintName = value == "Yes";
                        break;
                }
            }
            return result;
        }
    }

    /// <summary>
    /// 计算 PowerDesigner 表头和字段行位置。
    /// </summary>
    internal static class PdmTableLayout
    {
        // 8pt 原生样例使用更紧凑的行距；较大字号按字体高度排版。
        /// <summary>
        /// 获取表头高度。
        /// </summary>
        /// <param name="height">表的原始高度。</param>
        /// <param name="fontSize">图形字号。</param>
        /// <returns>按字体规则计算的表头高度。</returns>
        public static int HeaderHeight(int height, int fontSize) =>
            Math.Min(height, fontSize <= 800 ? fontSize * 4 / 5 + 600 : fontSize * 3 / 2);

        /// <summary>
        /// 获取指定字段行的基线。
        /// </summary>
        /// <param name="top">表顶部坐标。</param>
        /// <param name="header">表头高度。</param>
        /// <param name="fontSize">图形字号。</param>
        /// <param name="row">字段行号。</param>
        /// <returns>指定字段行的文字基线。</returns>
        public static int ColumnBaseline(int top, int header, int fontSize, int row) =>
            fontSize <= 800 ? top + header + row * (fontSize * 4 / 5 + 300) :
                top + header + row * fontSize + (row - 1) * 160;
    }
}
