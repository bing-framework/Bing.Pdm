using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Bing.Pdm.Models.PhysicalDiagrams
{
    /// <summary>
    /// 物理图信息。
    /// </summary>
    public sealed class PhysicalDiagramInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置所属包标识。
        /// </summary>
        public string PackageId { get; set; }
        /// <summary>
        /// 获取或设置物理图的显示配置。
        /// </summary>
        public string DisplayPreferences { get; set; }

        /// <summary>
        /// 获取顶层图形符号集合。
        /// </summary>
        public List<DiagramSymbolInfo> Symbols { get; } = new List<DiagramSymbolInfo>();

        /// <summary>
        /// 获取物理图中的全部图形符号。
        /// </summary>
        [JsonIgnore]
        public IEnumerable<DiagramSymbolInfo> AllSymbols => Symbols.SelectMany(x => x.SelfAndDescendants());
    }

    /// <summary>
    /// 物理图图形符号信息。
    /// </summary>
    public sealed class DiagramSymbolInfo
    {
        /// <summary>
        /// 获取或设置符号标识。
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// 获取或设置符号种类。
        /// </summary>
        public string Kind { get; set; }
        /// <summary>
        /// 获取或设置规范化对象标识。
        /// </summary>
        public string ObjectId { get; set; }
        /// <summary>
        /// 获取或设置工作区内的图元目标地址。
        /// </summary>
        public PdmObjectAddress ObjectAddress { get; set; }
        /// <summary>
        /// 获取或设置原始对象引用。
        /// </summary>
        public string RawObjectRef { get; set; }
        /// <summary>
        /// 获取或设置连线起点符号标识。
        /// </summary>
        public string SourceSymbolId { get; set; }
        /// <summary>
        /// 获取或设置连线终点符号标识。
        /// </summary>
        public string DestinationSymbolId { get; set; }
        /// <summary>
        /// 获取或设置符号文本。
        /// </summary>
        public string Text { get; set; }
        /// <summary>
        /// 获取或设置符号类型。
        /// </summary>
        public string SymbolType { get; set; }
        /// <summary>
        /// 获取或设置线条颜色。
        /// </summary>
        public string LineColor { get; set; }
        /// <summary>
        /// 获取或设置填充颜色。
        /// </summary>
        public string FillColor { get; set; }
        /// <summary>
        /// 获取或设置阴影颜色。
        /// </summary>
        public string ShadowColor { get; set; }
        /// <summary>
        /// 获取或设置原始字体列表。
        /// </summary>
        public string FontList { get; set; }
        /// <summary>
        /// 获取或设置图形字体。
        /// </summary>
        public string FontName { get; set; }
        /// <summary>
        /// 获取或设置文字样式。
        /// </summary>
        public string TextStyle { get; set; }
        /// <summary>
        /// 获取或设置虚线样式。
        /// </summary>
        public string DashStyle { get; set; }
        /// <summary>
        /// 获取或设置原始显示选项。
        /// </summary>
        public string DisplayPreferences { get; set; }
        /// <summary>
        /// 获取或设置圆角样式。
        /// </summary>
        public string CornerStyle { get; set; }
        /// <summary>
        /// 获取或设置箭头样式。
        /// </summary>
        public string ArrowStyle { get; set; }
        /// <summary>
        /// 获取或设置画笔样式。
        /// </summary>
        public string PenStyle { get; set; }
        /// <summary>
        /// 获取或设置线宽。
        /// </summary>
        public string LineWidth { get; set; }
        /// <summary>
        /// 获取或设置画刷样式。
        /// </summary>
        public string BrushStyle { get; set; }
        /// <summary>
        /// 获取或设置渐变填充模式。
        /// </summary>
        public string GradientFillMode { get; set; }
        /// <summary>
        /// 获取或设置渐变终点颜色。
        /// </summary>
        public string GradientEndColor { get; set; }
        /// <summary>
        /// 获取或设置原始 RTF 文本。
        /// </summary>
        public string RawText { get; set; }
        /// <summary>
        /// 获取 RTF 文本的样式分段。
        /// </summary>
        public List<PdmRichTextSegmentInfo> RichTextSegments { get; } = new List<PdmRichTextSegmentInfo>();
        /// <summary>
        /// 获取或设置原始绘制顺序。
        /// </summary>
        public int DrawOrder { get; set; }
        /// <summary>
        /// 获取或设置符号矩形范围。
        /// </summary>
        public DiagramRectangleInfo Rect { get; set; }
        /// <summary>
        /// 获取折线拐点集合。
        /// </summary>
        public List<DiagramPointInfo> Points { get; } = new List<DiagramPointInfo>();
        /// <summary>
        /// 获取子符号集合。
        /// </summary>
        public List<DiagramSymbolInfo> SubSymbols { get; } = new List<DiagramSymbolInfo>();

        /// <summary>
        /// 枚举当前符号及其全部后代符号。
        /// </summary>
        /// <returns>当前符号和后代符号。</returns>
        public IEnumerable<DiagramSymbolInfo> SelfAndDescendants()
        {
            yield return this;
            foreach (var child in SubSymbols.SelectMany(x => x.SelfAndDescendants()))
                yield return child;
        }
    }

    /// <summary>
    /// RTF 文本的一个连续样式片段。
    /// </summary>
    public sealed class PdmRichTextSegmentInfo
    {
        /// <summary>
        /// 获取或设置片段文本。
        /// </summary>
        public string Text { get; set; }
        /// <summary>
        /// 获取或设置是否粗体。
        /// </summary>
        public bool Bold { get; set; }
        /// <summary>
        /// 获取或设置是否斜体。
        /// </summary>
        public bool Italic { get; set; }
        /// <summary>
        /// 获取或设置字号（磅）。
        /// </summary>
        public int? FontSize { get; set; }
        /// <summary>
        /// 获取或设置片段字体。
        /// </summary>
        public string FontFamily { get; set; }
        /// <summary>
        /// 获取或设置片段前景色。
        /// </summary>
        public string ForegroundColor { get; set; }
        /// <summary>
        /// 获取或设置是否显示下划线。
        /// </summary>
        public bool Underline { get; set; }
        /// <summary>
        /// 获取或设置段落对齐方式。
        /// </summary>
        /// <remarks>取值为 Left、Center、Right 或 Justify。</remarks>
        public string ParagraphAlignment { get; set; }
    }

    /// <summary>
    /// 图形符号的矩形范围。
    /// </summary>
    public sealed class DiagramRectangleInfo
    {
        /// <summary>
        /// 获取或设置左侧坐标。
        /// </summary>
        public int X1 { get; set; }
        /// <summary>
        /// 获取或设置顶部坐标。
        /// </summary>
        public int Y1 { get; set; }
        /// <summary>
        /// 获取或设置右侧坐标。
        /// </summary>
        public int X2 { get; set; }
        /// <summary>
        /// 获取或设置底部坐标。
        /// </summary>
        public int Y2 { get; set; }
    }

    /// <summary>
    /// 物理图折线点。
    /// </summary>
    public sealed class DiagramPointInfo
    {
        /// <summary>
        /// 获取或设置横坐标。
        /// </summary>
        public int X { get; set; }
        /// <summary>
        /// 获取或设置纵坐标。
        /// </summary>
        public int Y { get; set; }
    }
}
