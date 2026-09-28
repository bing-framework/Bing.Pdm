namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 所有者信息。
    /// </summary>
    public sealed class PdmOwnerInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置所有者类型。
        /// </summary>
        public string Stereotype { get; set; }
    }
}
