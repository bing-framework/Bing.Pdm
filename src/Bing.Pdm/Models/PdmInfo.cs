using System.Collections.Generic;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 信息
    /// </summary>
    public class PdmInfo
    {
        /// <summary>
        /// 架构集合
        /// </summary>
        public IList<SchemaInfo> Schemas { get; set; } = new List<SchemaInfo>();

        /// <summary>
        /// 表集合
        /// </summary>
        public IList<TableInfo> Tables { get; set; } = new List<TableInfo>();

        /// <summary>
        /// 视图集合
        /// </summary>
        public IList<ViewInfo> Views { get; set; } = new List<ViewInfo>();
    }
}
