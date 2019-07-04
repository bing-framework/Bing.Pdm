using System;

namespace Bing.Pdm.Models.PhysicalDiagrams
{
    /// <summary>
    /// 表符号信息
    /// </summary>
    public class TableSymbolInfo
    {
        /// <summary>
        /// 表符号标识
        /// </summary>
        public string TableSymbolId { get; set; }

        /// <summary>
        /// 创建日期
        /// </summary>
        public DateTime CreationDate { get; set; }

        /// <summary>
        /// 修改日期
        /// </summary>
        public DateTime ModificationDate { get; set; }

        /// <summary>
        /// 矩形坐标
        /// </summary>
        public string Rect { get; set; }

        /// <summary>
        /// 线颜色
        /// </summary>
        public string LineColor { get; set; }

        /// <summary>
        /// 填充颜色
        /// </summary>
        public string FillColor { get; set; }

        /// <summary>
        /// 阴影颜色
        /// </summary>
        public string ShadowColor { get; set; }

        /// <summary>
        /// 字体列表
        /// </summary>
        public string FontList { get; set; }

        /// <summary>
        /// 画笔样式
        /// </summary>
        public string BrushStyle { get; set; }

        /// <summary>
        /// 渐变填充模式
        /// </summary>
        public string GradientFillMode { get; set; }

        /// <summary>
        /// 渐变结束颜色
        /// </summary>
        public string GradientEndColor { get; set; }

        /// <summary>
        /// 数据表标识
        /// </summary>
        public string TableId { get; set; }
    }
}
