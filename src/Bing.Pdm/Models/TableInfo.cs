using System.Collections.Generic;
using System.Linq;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 表信息
    /// </summary>
    public class TableInfo : PdmCommonInfo
    {
        /// <summary>
        /// 表标识
        /// </summary>
        public string TableId { get; set; }

        /// <summary>
        /// 注释
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 字段列表
        /// </summary>
        public IList<ColumnInfo> Columns { get; private set; }

        /// <summary>
        /// 键列表
        /// </summary>
        public IList<KeyInfo> Keys { get; private set; }

        /// <summary>
        /// 索引列表
        /// </summary>
        public IList<IndexInfo> Indexes { get; private set; }

        /// <summary>
        /// 主键Key代码.=>KeyId
        /// </summary>
        public string PrimaryKeyRefCode { get; set; }

        /// <summary>
        /// 主关键字
        /// </summary>
        public KeyInfo PrimaryKey => Keys.FirstOrDefault(key => key.KeyId == PrimaryKeyRefCode);

        /// <summary>
        /// 物理选项
        /// </summary>
        public string PhysicalOptions { get; set; }

        /// <summary>
        /// 初始化一个<see cref="TableInfo"/>类型的实例
        /// </summary>
        public TableInfo()
        {
            Keys = new List<KeyInfo>();
            Columns = new List<ColumnInfo>();
            Indexes = new List<IndexInfo>();
        }

        /// <summary>
        /// 添加字段
        /// </summary>
        /// <param name="column">字段信息</param>
        public void AddColumn(ColumnInfo column)
        {
            if (Columns == null)
                Columns = new List<ColumnInfo>();
            Columns.Add(column);
        }

        /// <summary>
        /// 添加键
        /// </summary>
        /// <param name="key">键信息</param>
        public void AddKey(KeyInfo key)
        {
            if (Keys == null)
                Keys = new List<KeyInfo>();
            Keys.Add(key);
        }

        /// <summary>
        /// 添加索引
        /// </summary>
        /// <param name="index">索引信息</param>
        public void AddIndex(IndexInfo index)
        {
            if (Indexes == null)
                Indexes = new List<IndexInfo>();
            Indexes.Add(index);
        }
    }
}
