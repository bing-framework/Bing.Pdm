using System.Collections.Generic;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 引用
    /// </summary>
    public class PdmReference
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
        /// 父表
        /// </summary>
        public PdmTable ParentTable { get; set; }

        /// <summary>
        /// 子表
        /// </summary>
        public PdmTable ChildTable { get; set; }

        /// <summary>
        /// 更新约束
        /// </summary>
        public int UpdateConstraint { get; set; } = 1;

        /// <summary>
        /// 删除约束
        /// </summary>
        public int DeleteConstraint { get; set; } = 1;

        /// <summary>
        /// 实现类型
        /// </summary>
        public string ImplementationType { get; set; }

        /// <summary>
        /// PDM 引用关联列表
        /// </summary>
        public List<PdmReferenceJoin> Joins { get; set; } = new List<PdmReferenceJoin>();
    }
}
