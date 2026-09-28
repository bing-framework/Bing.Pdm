namespace Bing.Pdm.Models.Tables
{
    /// <summary>
    /// 表列信息。
    /// </summary>
    public sealed class ColumnInfo : PdmCommonInfo, IDescription
    {
        /// <summary>
        /// 获取或设置列描述。
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// 获取或设置数据库类型。
        /// </summary>
        public string DataType { get; set; }
        /// <summary>
        /// 获取或设置数据长度。
        /// </summary>
        public string Length { get; set; }
        /// <summary>
        /// 获取或设置数据精度。
        /// </summary>
        public string Precision { get; set; }
        /// <summary>
        /// 获取或设置默认值。
        /// </summary>
        public string DefaultValue { get; set; }
        /// <summary>
        /// 获取或设置是否自增。
        /// </summary>
        public bool Identity { get; set; }
        /// <summary>
        /// 获取或设置是否必填。
        /// </summary>
        public bool Mandatory { get; set; }
        /// <summary>
        /// 获取或设置是否为主键列。
        /// </summary>
        public bool PrimaryKey { get; set; }
    }
}
