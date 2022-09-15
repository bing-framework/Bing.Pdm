using System.Collections.Generic;

namespace Bing.Pdm.Models.References
{
    /// <summary>
    /// 引用信息
    /// </summary>
    public class ReferenceInfo : PdmCommonInfo
    {
        /// <summary>
        /// 父表引用
        /// </summary>
        [ChildObject("c:ParentTable")]
        public RefInfo ParentTable { get; set; }

        /// <summary>
        /// 子表引用
        /// </summary>
        [ChildObject("c:ChildTable")]
        public RefInfo ChildTable { get; set; }

        /// <summary>
        /// 引用关联集合
        /// </summary>
        [ChildObject("c:Joins", typeof(ReferenceJoinInfo))]
        public List<ReferenceJoinInfo> ReferenceJoinInfos { get; set; }
    }
}
