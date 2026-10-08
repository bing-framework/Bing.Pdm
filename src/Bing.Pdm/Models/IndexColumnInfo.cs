namespace Bing.Pdm.Models
{
    /// <summary>
    /// 索引列信息。
    /// </summary>
    public sealed class IndexColumnInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置列标识。
        /// </summary>
        public string ColumnId { get; set; }
        /// <summary>
        /// 获取或设置工作区内的列地址。
        /// </summary>
        public PdmObjectAddress ColumnAddress { get; set; }
    }
}
