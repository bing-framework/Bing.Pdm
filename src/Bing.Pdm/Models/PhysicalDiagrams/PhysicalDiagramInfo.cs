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
