using System.Collections.Generic;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 信息
    /// </summary>
    public class PdmInfo : PdmCommonInfo
    {
        /// <summary>
        /// PDM标识
        /// </summary>
        public string PdmId { get; set; }

        /// <summary>
        /// 作者
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// 版本号
        /// </summary>
        public string Version { get; set; }

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

        /// <summary>
        /// 包集合
        /// </summary>
        public IList<PackageInfo> Packages { get; set; } = new List<PackageInfo>();
    }
}
