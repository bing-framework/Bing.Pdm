namespace Bing.Pdm.Models.PhysicalDiagrams
{
    /// <summary>
    /// 物理图信息
    /// </summary>
    public class PhysicalDiagramInfo : PdmCommonInfo
    {
        /// <summary>
        /// 物理图标识
        /// </summary>
        public string PhysicalDiagramId { get; set; }

        /// <summary>
        /// 显示引用
        /// </summary>
        public string DisplayPreferences { get; set; }

        /// <summary>
        /// 页面尺寸
        /// </summary>
        public string PagerSize { get; set; }

        /// <summary>
        /// 页面边距
        /// </summary>
        public string PageMargins { get; set; }

        /// <summary>
        /// 页面方向
        /// </summary>
        public int PageOrientation { get; set; }

        /// <summary>
        /// 纸张类源
        /// </summary>
        public int PaperSource { get; set; }
    }
}
