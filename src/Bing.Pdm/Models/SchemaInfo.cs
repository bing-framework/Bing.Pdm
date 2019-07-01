namespace Bing.Pdm.Models
{
    /// <summary>
    /// 架构信息
    /// </summary>
    public class SchemaInfo : PdmCommonInfo
    {
        /// <summary>
        /// 架构标识
        /// </summary>
        public string SchemaId { get; set; }

        /// <summary>
        /// 声明类型
        /// </summary>
        public string StereoType { get; set; }
    }
}
