namespace Bing.Pdm.Models
{
    /// <summary>
    /// 数据库模式信息。
    /// </summary>
    public sealed class SchemaInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置模式标识。
        /// </summary>
        public string SchemaId { get; set; }
        /// <summary>
        /// 获取或设置模式类型。
        /// </summary>
        public string StereoType { get; set; }
    }
}
