using System.Collections.Generic;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 约束键（主键、外键、唯一键）
    /// </summary>
    public class PdmKey
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
        /// 约束名称
        /// </summary>
        public string ConstraintName { get; set; }

        /// <summary>
        /// PDM 列结构列表
        /// </summary>
        public List<PdmColumn> Columns { get; set; } = new List<PdmColumn>();
    }
}
