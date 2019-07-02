using Newtonsoft.Json;

namespace Bing.Pdm.Models.Tables
{
    /// <summary>
    /// 表列信息
    /// </summary>
    public class ColumnInfo : PdmCommonInfo, IComment, IDescription
    {
        /// <summary>
        /// 列标识
        /// </summary>
        public string ColumnId { get; set; }

        /// <summary>
        /// 注释
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// 数据长度
        /// </summary>
        public string Length { get; set; }

        /// <summary>
        /// 是否自增量
        /// </summary>
        public bool Identity { get; set; }

        /// <summary>
        /// 是否禁止为空
        /// </summary>
        public bool Mandatory { get; set; }

        /// <summary>
        /// 精度
        /// </summary>
        public string Precision { get; set; }

        /// <summary>
        /// 扩展属性
        /// </summary>
        public string ExtendedAttributesText { get; set; }

        /// <summary>
        /// 物理选项
        /// </summary>
        public string PhysicalOptions { get; set; }

        /// <summary>
        /// 所有者表信息
        /// </summary>
        [JsonIgnore]
        public TableInfo OwnerTable { get; private set; }

        /// <summary>
        /// 是否主键
        /// </summary>
        public bool IsPrimaryKey
        {
            get
            {
                var key = OwnerTable.PrimaryKey;
                return key != null && key.ColumnRefIds.Contains(ColumnId);
            }
        }

        /// <summary>
        /// 初始化一个<see cref="ColumnInfo"/>类型的实例
        /// </summary>
        /// <param name="table">所有者表信息</param>
        public ColumnInfo(TableInfo table)
        {
            OwnerTable = table;
        }
    }
}
