namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM Shortcut 信息。
    /// </summary>
    public sealed class PdmShortcutInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置所属包标识。
        /// </summary>
        public string PackageId { get; set; }
        /// <summary>
        /// 获取或设置原始目标标识。
        /// </summary>
        public string TargetId { get; set; }
        /// <summary>
        /// 获取或设置目标对象类型标识。
        /// </summary>
        public string TargetClassId { get; set; }
        /// <summary>
        /// 获取或设置目标包路径。
        /// </summary>
        public string TargetPackagePath { get; set; }
        /// <summary>
        /// 获取或设置目标类型名称。
        /// </summary>
        public string TargetKind { get; set; }
        /// <summary>
        /// 获取或设置解析后的目标标识。
        /// </summary>
        public string ResolvedTargetId { get; set; }
    }
}
