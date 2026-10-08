using System.Collections.Generic;

namespace Bing.Pdm.Models.Keys
{
    /// <summary>
    /// 表键的信息。
    /// </summary>
    public sealed class KeyInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取键包含的列标识。
        /// </summary>
        public List<string> ColumnIds { get; } = new List<string>();
        /// <summary>
        /// 获取工作区内的键列地址。
        /// </summary>
        public List<PdmObjectAddress> ColumnAddresses { get; } = new List<PdmObjectAddress>();
    }
}
