using System.Collections.Generic;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 索引
    /// </summary>
    public class PdmIndex
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
        /// PDM 列结构列表
        /// </summary>
        public List<PdmColumn> Columns { get; set; } = new List<PdmColumn>();
    }
}
