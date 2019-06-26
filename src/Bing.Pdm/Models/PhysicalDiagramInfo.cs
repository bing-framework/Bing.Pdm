using System;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 物理图信息
    /// </summary>
    public class PhysicalDiagramInfo
    {
        /// <summary>
        /// 物理图标识
        /// </summary>
        public string PhysicalDiagramId { get; set; }

        /// <summary>
        /// 对象标识
        /// </summary>
        public string ObjectId { get; set; }

        /// <summary>
        /// 物理图名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 物理图代码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 创建日期
        /// </summary>
        public DateTime CreationDate { get; set; }

        /// <summary>
        /// 创建人
        /// </summary>
        public string Creator { get; set; }

        /// <summary>
        /// 修改日期
        /// </summary>
        public DateTime ModificationDate { get; set; }

        /// <summary>
        /// 修改人
        /// </summary>
        public string Modifier { get; set; }

        /// <summary>
        /// 显示引用
        /// </summary>
        public string DisplayPreferences { get; set; }

        /// <summary>
        /// 页面尺寸
        /// </summary>
        public string PagerSize { get; set; }

        /// <summary>
        /// 页面编剧
        /// </summary>
        public string PageMargins { get; set; }
    }
}
