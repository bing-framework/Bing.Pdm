using System.Collections.Generic;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 包信息
    /// </summary>
    public class PackageInfo : PdmCommonInfo
    {
        /// <summary>
        /// 包标识
        /// </summary>
        public string PackageId { get; set; }

        /// <summary>
        /// 包选项文本
        /// </summary>
        public string PackageOptionsText { get; set; }

        /// <summary>
        /// 物理图集合
        /// </summary>
        public IList<PhysicalDiagramInfo> PhysicalDiagrams { get; private set; } = new List<PhysicalDiagramInfo>();
    }
}
