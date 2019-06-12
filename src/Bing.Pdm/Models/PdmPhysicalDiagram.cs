using System.Collections.Generic;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 物理图
    /// </summary>
    public class PdmPhysicalDiagram
    {
        /// <summary>
        /// 系统编号
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// PDM 表结构
        /// </summary>
        public List<PdmTable> Tables { get; set; } = new List<PdmTable>();
    }
}
