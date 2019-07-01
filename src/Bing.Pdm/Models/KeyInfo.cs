using System.Collections.Generic;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 键信息
    /// </summary>
    public class KeyInfo : PdmCommonInfo
    {
        /// <summary>
        /// 所有者表信息
        /// </summary>
        private TableInfo _ownerTable = null;

        /// <summary>
        /// 键标识
        /// </summary>
        public string KeyId { get; set; }

        /// <summary>
        /// Key涉及的列
        /// </summary>
        public IList<ColumnInfo> Columns { get; private set; }

        /// <summary>
        /// 列引用标识列表。根据此可访问到列信息对应列的ColumnId
        /// </summary>
        public List<string> ColumnRefIds { get; private set; }

        /// <summary>
        /// 初始化一个<see cref="KeyInfo"/>类型的实例
        /// </summary>
        /// <param name="table">所有者表信息</param>
        public KeyInfo(TableInfo table)
        {
            _ownerTable = table;
            ColumnRefIds = new List<string>();
        }

        /// <summary>
        /// 添加关联列
        /// </summary>
        /// <param name="column">列信息</param>
        public void AddColumn(ColumnInfo column)
        {
            if (Columns == null)
                Columns = new List<ColumnInfo>();
            Columns.Add(column);
        }
    }
}
