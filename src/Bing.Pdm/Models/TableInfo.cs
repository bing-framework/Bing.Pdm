using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 表信息
    /// </summary>
    public class TableInfo
    {
        /// <summary>
        /// 表标识
        /// </summary>
        public string TableId { get; set; }

        /// <summary>
        /// 对象标识
        /// </summary>
        public string ObjectId { get; set; }

        /// <summary>
        /// 表名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 表代码。对应数据库表名
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 创建日期
        /// </summary>
        public DateTime CreationDate { get; set; }

        /// <summary>
        /// 创建人
        /// </summary>
        public string Creator { get; set; }

        /// <summary>
        /// 修改日期
        /// </summary>
        public DateTime ModificationDate { get; set; }

        /// <summary>
        /// 修改人
        /// </summary>
        public string Modifier { get; set; }

        /// <summary>
        /// 注释
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 表列集合
        /// </summary>
        public IList<ColumnInfo> Columns { get; private set; }

        /// <summary>
        /// 表Key集合
        /// </summary>
        public IList<PdmKey> Keys { get; private set; }

        /// <summary>
        /// 主键Key代码.=>KeyId
        /// </summary>
        public string PrimaryKeyRefCode { get; set; }

        /// <summary>
        /// 主关键字
        /// </summary>
        public PdmKey PrimaryKey => Keys.FirstOrDefault(key => key.KeyId == PrimaryKeyRefCode);

        /// <summary>
        /// 物理选项
        /// </summary>
        public string PhysicalOptions { get; set; }

        /// <summary>
        /// 初始化一个<see cref="TableInfo"/>类型的实例
        /// </summary>
        public TableInfo()
        {
            Keys = new List<PdmKey>();
            Columns = new List<ColumnInfo>();
        }

        /// <summary>
        /// 添加列
        /// </summary>
        /// <param name="column">列信息</param>
        public void AddColumn(ColumnInfo column)
        {
            if (Columns == null)
                Columns = new List<ColumnInfo>();
            Columns.Add(column);
        }

        /// <summary>
        /// 添加Key
        /// </summary>
        /// <param name="key">Key</param>
        public void AddKey(PdmKey key)
        {
            if (Keys == null)
                Keys = new List<PdmKey>();
            Keys.Add(key);
        }
    }
}
