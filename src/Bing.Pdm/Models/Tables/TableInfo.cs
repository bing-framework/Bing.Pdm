using System.Collections.Generic;
using Bing.Pdm.Models.Keys;

namespace Bing.Pdm.Models.Tables
{
    /// <summary>
    /// 数据库表信息。
    /// </summary>
    public sealed class TableInfo : PdmCommonInfo, IDescription
    {
        /// <summary>
        /// 获取或设置表描述。
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// 获取或设置所属包标识。
        /// </summary>
        public string PackageId { get; set; }
        /// <summary>
        /// 获取或设置所有者标识。
        /// </summary>
        public string OwnerId { get; set; }
        /// <summary>
        /// 获取或设置模式标识。
        /// </summary>
        public string SchemaId { get; set; }
        /// <summary>
        /// 获取或设置主键标识。
        /// </summary>
        public string PrimaryKeyId { get; set; }
        /// <summary>
        /// 获取表列集合。
        /// </summary>
        public List<ColumnInfo> Columns { get; } = new List<ColumnInfo>();
        /// <summary>
        /// 获取表键集合。
        /// </summary>
        public List<KeyInfo> Keys { get; } = new List<KeyInfo>();
        /// <summary>
        /// 获取表索引集合。
        /// </summary>
        public List<IndexInfo> Indexes { get; } = new List<IndexInfo>();
        /// <summary>
        /// 获取表触发器集合。
        /// </summary>
        public List<PdmTriggerInfo> Triggers { get; } = new List<PdmTriggerInfo>();
    }
}
