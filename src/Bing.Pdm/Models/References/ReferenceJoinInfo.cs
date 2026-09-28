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
        /// 获取或设置子表列标识。
        /// </summary>
        public string ChildColumnId { get; set; }
    }
}
