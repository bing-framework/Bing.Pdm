using System.Collections.Generic;

namespace Bing.Pdm.Models.References
{
    /// <summary>
    /// 引用信息
    /// </summary>
    public class ReferenceInfo : PdmCommonInfo
    {
        /// <summary>
        /// 引用标识
        /// </summary>
        public string ReferenceId { get; set; }

        /// <summary>
        /// 基数
        /// </summary>
        public string Cardinality { get; set; }

        /// <summary>
        /// 父表标识
        /// </summary>
        public string ParentTableId { get; set; }

        /// <summary>
        /// 子表标识
        /// </summary>
        public string ChildTableId { get; set; }

        /// <summary>
        /// 引用关联集合
        /// </summary>
        public IList<ReferenceJoinInfo> Joins { get; private set; } = new List<ReferenceJoinInfo>();
    }
}
