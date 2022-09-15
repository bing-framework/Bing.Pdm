namespace Bing.Pdm.Models.References
{
    /// <summary>
    /// 引用关联信息
    /// </summary>
    public class ReferenceJoinInfo : PdmCommonInfo
    {
        /// <summary>
        /// 父表列
        /// </summary>
        [ChildObject("c:Object1")]
        public RefInfo ParentTableColumn { get; set; }

        /// <summary>
        /// 子表列引用
        /// </summary>
        [ChildObject("c:Object1")]
        public RefInfo ChildTableColumn { get; set; }
    }
}
