﻿using System;
using System.Collections.Generic;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 关键字
    /// </summary>
    public class PdmKey
    {
        /// <summary>
        /// 所有者表信息
        /// </summary>
        private TableInfo _ownerTable = null;

        /// <summary>
        /// 关键字标识
        /// </summary>
        public string KeyId { get; set; }

        /// <summary>
        /// 对象标识
        /// </summary>
        public string ObjectId { get; set; }

        /// <summary>
        /// Key名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Key代码。对应数据库中的Key
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
        /// Key涉及的列
        /// </summary>
        public IList<ColumnInfo> Columns { get; private set; }

        /// <summary>
        /// Key设计的列代码。根据此可访问到列信息对应列的ColumnId
        /// </summary>
        public List<string> ColumnObjCodes { get; private set; }

        /// <summary>
        /// 初始化一个<see cref="PdmKey"/>类型的实例
        /// </summary>
        /// <param name="table">所有者表信息</param>
        public PdmKey(TableInfo table)
        {
            _ownerTable = table;
            ColumnObjCodes = new List<string>();
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