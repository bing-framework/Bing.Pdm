namespace Bing.Pdm.Models.References
{
    /// <summary>
    /// 引用中的列关联信息。
    /// </summary>
    public sealed class ReferenceJoinInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置父表列标识。
        /// </summary>
        public string ParentColumnId { get; set; }
        /// <summary>
        /// 获取或设置工作区内的父列地址。
        /// </summary>
        public PdmObjectAddress ParentColumnAddress { get; set; }
        /// <summary>
        /// 获取或设置子表列标识。
        /// </summary>
        public string ChildColumnId { get; set; }
        /// <summary>
        /// 获取或设置工作区内的子列地址。
        /// </summary>
        public PdmObjectAddress ChildColumnAddress { get; set; }
    }
}
