using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bing.Pdm.Models.References
{
    /// <summary>
    /// 表引用信息。
    /// </summary>
    public sealed class ReferenceInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置原始父表引用。
        /// </summary>
        [JsonIgnore]
        public RefInfo ParentTable { get; set; }
        /// <summary>
        /// 获取或设置原始子表引用。
        /// </summary>
        [JsonIgnore]
        public RefInfo ChildTable { get; set; }
        /// <summary>
        /// 获取或设置规范化父表标识。
        /// </summary>
        public string ParentTableId { get; set; }
        /// <summary>
        /// 获取或设置规范化子表标识。
        /// </summary>
        public string ChildTableId { get; set; }
        /// <summary>
        /// 获取或设置原始父表引用标识。
        /// </summary>
        public string RawParentTableRef { get; set; }
        /// <summary>
        /// 获取或设置原始子表引用标识。
        /// </summary>
        public string RawChildTableRef { get; set; }
        /// <summary>
        /// 获取或设置父键标识。
        /// </summary>
        public string ParentKeyId { get; set; }
        /// <summary>
        /// 获取或设置基数说明。
        /// </summary>
        public string Cardinality { get; set; }
        /// <summary>
        /// 获取引用列关联集合。
        /// </summary>
        public List<ReferenceJoinInfo> Joins { get; } = new List<ReferenceJoinInfo>();
        /// <summary>
        /// 获取兼容名称的引用列关联集合。
        /// </summary>
        [JsonIgnore]
        public List<ReferenceJoinInfo> ReferenceJoinInfos => Joins;
    }
}
