using System.Collections.Generic;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 表索引信息。
    /// </summary>
    public sealed class IndexInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置索引是否唯一。
        /// </summary>
        public bool Unique { get; set; }

        /// <summary>
        /// 获取索引包含的列标识。
        /// </summary>
        public List<string> ColumnIds { get; } = new List<string>();

        /// <summary>
        /// 获取索引列信息。
        /// </summary>
        public List<IndexColumnInfo> IndexColumns { get; } = new List<IndexColumnInfo>();
    }
}
