using System;

namespace Bing.Pdm.Models.PhysicalDiagrams
{
    /// <summary>
    /// 引用符号信息
    /// </summary>
    public class ReferenceSymbolInfo
    {
        /// <summary>
        /// 引用符号标识
        /// </summary>
        public string ReferenceSymbolId { get; set; }

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
        /// 点阵
        /// </summary>
        public string ListOfPoints { get; set; }

        /// <summary>
        /// 边角样式
        /// </summary>
        public string CornerStyle { get; set; }

        /// <summary>
        /// 箭头样式
        /// </summary>
        public string ArrowStyle { get; set; }

        /// <summary>
        /// 线颜色
        /// </summary>
        public string LineColor { get; set; }

        /// <summary>
        /// 字体列表
        /// </summary>
        public string FontList { get; set; }

        /// <summary>
        /// 源表符号标识
        /// </summary>
        public string SourceTableSymbolId { get; set; }

        /// <summary>
        /// 目标表符号标识
        /// </summary>
        public string DestinationTableSymbolId { get; set; }

        /// <summary>
        /// 引用标识
        /// </summary>
        public string ReferenceId { get; set; }
    }
}
