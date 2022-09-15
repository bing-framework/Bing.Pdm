using System.Collections.Generic;
using Bing.Pdm.Models.References;

namespace Bing.Pdm.Models.Keys
{
    /// <summary>
    /// 键信息
    /// </summary>
    public class KeyInfo : PdmCommonInfo
    {
        /// <summary>
        /// Key涉及的列
        /// </summary>
        [ChildObject("c:Key.ColumnInfos", typeof(RefInfo))]
        public List<RefInfo> Columns { get; set; }
    }
}
