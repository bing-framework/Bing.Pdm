using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Pdm.Models;

namespace Bing.Pdm
{
    /// <summary>
    /// 物理图渲染风格。
    /// </summary>
    public enum PdmDiagramStyle
    {
        /// <summary>
        /// 沿用现有简化显示。
        /// </summary>
        Simplified,
        /// <summary>
        /// 使用 PowerDesigner 样式信息。
        /// </summary>
        PowerDesigner
    }

    /// <summary>
    /// 图形文字的字体测量请求。
    /// </summary>
    public sealed class PdmDiagramTextMeasureInfo
    {
        /// <summary>
        /// 获取或设置待测文本。
        /// </summary>
        public string Text { get; set; }
        /// <summary>
        /// 获取或设置字体名称。
        /// </summary>
        public string FontFamily { get; set; }
        /// <summary>
        /// 获取或设置图形坐标中的字号。
        /// </summary>
        public int FontSize { get; set; }
        /// <summary>
        /// 获取或设置是否粗体。
        /// </summary>
        public bool Bold { get; set; }
        /// <summary>
        /// 获取或设置是否斜体。
        /// </summary>
        public bool Italic { get; set; }
    }

    /// <summary>
    /// 物理图渲染选项。
    /// </summary>
    public sealed class PdmDiagramRenderOptions
    {
        /// <summary>
        /// 获取或设置渲染风格。
        /// </summary>
        public PdmDiagramStyle Style { get; set; } = PdmDiagramStyle.Simplified;
        /// <summary>
        /// 获取或设置调用方提供的字体可用性检查。
        /// </summary>
        public Func<string, bool> FontAvailable { get; set; }
        /// <summary>
        /// 获取或设置字体宽度测量器。
        /// </summary>
        /// <remarks>返回值和字号均使用 PDM 图形坐标单位；未提供或返回无效值时使用估算并记录诊断。</remarks>
        public Func<PdmDiagramTextMeasureInfo, double> MeasureText { get; set; }
        /// <summary>
        /// 获取或设置自定义列筛选器。
        /// </summary>
        /// <remarks>参数依次为表、列和原始过滤表达式；返回空值表示无法解释该表达式。</remarks>
        public Func<Bing.Pdm.Models.Tables.TableInfo, Bing.Pdm.Models.Tables.ColumnInfo, string, bool?> ColumnFilter { get; set; }
        /// <summary>
        /// 获取渲染过程产生的诊断。
        /// </summary>
        public List<PdmDiagnostic> Diagnostics { get; } = new List<PdmDiagnostic>();
        /// <summary>
        /// 获取当前图形是否具备原生视觉对照条件。
        /// </summary>
        public bool CanCompareToNative => !Diagnostics.Any(x =>
            x.Code == "MISSING_DIAGRAM_FONT" || x.Code == "FONT_AVAILABILITY_UNVERIFIED" ||
            x.Code == "UNSUPPORTED_DIAGRAM_STYLE" || x.Code == "UNRESOLVED_REFERENCE_LABEL" || x.Code == "APPROXIMATE_FONT_METRICS");
    }
}
