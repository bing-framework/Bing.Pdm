using System.Collections.Generic;
using System.Linq;
using Bing.Pdm.Models.Keys;
using Bing.Pdm.Models.References;

namespace Bing.Pdm.Models.Tables
{
    /// <summary>
    /// 表信息
    /// </summary>
    public class TableInfo : PdmCommonInfo, IDescription
    {
        /// <summary>
        /// 表标识
        /// </summary>
        public string TableId { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 字段信息列表
        /// </summary>
        [ChildObject("c:ColumnInfos", typeof(ColumnInfo))]
        public List<ColumnInfo> ColumnInfos { get; set; }

        /// <summary>
        /// 键信息列表（主键）
        /// </summary>
        [ChildObject("c:KeyInfos", typeof(KeyInfo))]
        public List<KeyInfo> KeyInfos { get; set; }

        /// <summary>
        /// 主键列表
        /// </summary>
        [ChildObject("c:PrimaryKey", typeof(RefInfo))]
        public List<RefInfo> PrimaryKeys { get; set; }

        /// <summary>
        /// 索引信息列表
        /// </summary>
        [ChildObject("c:Indexes", typeof(KeyInfo))]
        public List<IndexInfo> IndexInfos { get; set; }

        /// <summary>
        /// 子表信息列表
        /// </summary>
        public List<ChildTableInfo> ChildTableInfos { get; set; }

        /// <summary>
        /// 引用表信息列表
        /// </summary>
        public List<ReferenceTableInfo> ReferenceTableInfos { get; set; }

        /// <summary>
        /// 表名
        /// </summary>
        private string _tableName;

        /// <summary>
        /// 是否已初始化表名
        /// </summary>
        private bool _isTableNameInit;

        /// <summary>
        /// 表名
        /// </summary>
        public string TableName
        {
            get
            {
                if (_isTableNameInit)
                    return _tableName;

                var index = Code.LastIndexOf('_');
                _tableName = Code.Substring(index + 1);
                _isTableNameInit = true;

                return _tableName;
            }
        }

        /// <summary>
        /// 主键代码
        /// </summary>
        private string _primaryKeyCode;

        /// <summary>
        /// 是否已初始化主键代码
        /// </summary>
        private bool _isPrimaryKeyCodeInit;

        /// <summary>
        /// 主键代码
        /// </summary>
        public string PrimaryKeyCode
        {
            get
            {
                if (_isPrimaryKeyCodeInit)
                    return _primaryKeyCode;

                var primaryKey = PrimaryKeys.FirstOrDefault();
                if (primaryKey == null)
                    return string.Empty;

                var keyInfo = KeyInfos.FirstOrDefault(x => x.Id == primaryKey.Ref);
                var key = keyInfo?.Columns.FirstOrDefault();
                if (key == null)
                    return string.Empty;

                var column = ColumnInfos.FirstOrDefault(x => x.Id == key.Ref);
                if (column == null)
                    return string.Empty;

                _primaryKeyCode = column.Code;
                _isPrimaryKeyCodeInit = true;

                return _primaryKeyCode;
            }
        }

        /// <summary>
        /// 物理选项
        /// </summary>
        public string PhysicalOptions { get; set; }
    }

    /// <summary>
    /// 子表信息
    /// </summary>
    public class ChildTableInfo
    {
        /// <summary>
        /// 外键
        /// </summary>
        public ColumnInfo ForeignKey { get; set; }

        /// <summary>
        /// 子表
        /// </summary>
        public TableInfo ChildTable { get; set; }

        /// <summary>
        /// 子属性名称
        /// </summary>
        public string ChildPropertyName { get; set; }
    }

    /// <summary>
    /// 引用表信息
    /// </summary>
    public class ReferenceTableInfo
    {
        /// <summary>
        /// 父表
        /// </summary>
        public TableInfo ParentTable { get; set; }

        /// <summary>
        /// 引用键
        /// </summary>
        public ColumnInfo ReferenceKey { get; set; }

        /// <summary>
        /// 外键
        /// </summary>
        public ColumnInfo ForeignKey { get; set; }

        /// <summary>
        /// 父属性名称
        /// </summary>
        public string ParentPropertyName { get; set; }
    }
}
