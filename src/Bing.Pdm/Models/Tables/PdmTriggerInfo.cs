using System.Collections.Generic;

namespace Bing.Pdm.Models.Tables
{
    /// <summary>
    /// 表触发器信息。
    /// </summary>
    public sealed class PdmTriggerInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置所属表标识。
        /// </summary>
        public string TableId { get; set; }
        /// <summary>
        /// 获取或设置触发时机。
        /// </summary>
        public string Timing { get; set; }
        /// <summary>
        /// 获取或设置触发事件。
        /// </summary>
        public string Event { get; set; }
        /// <summary>
        /// 获取或设置触发器正文。
        /// </summary>
        public string Body { get; set; }
        /// <summary>
        /// 获取尚未建模的原始属性。
        /// </summary>
        public Dictionary<string, string> RawAttributes { get; } = new Dictionary<string, string>();
    }
}
