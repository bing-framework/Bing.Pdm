using System;
using System.Collections.Generic;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 索引信息
    /// </summary>
    public class IndexInfo
    {
        /// <summary>
        /// 索引标识
        /// </summary>
        public string IndexId { get; set; }

        /// <summary>
        /// 对象标识
        /// </summary>
        public string ObjectId { get; set; }

        /// <summary>
        /// 索引名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 索引代码。对应数据库索引名
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
        /// 列引用标识列表
        /// </summary>
        public List<string> ColumnRefIds { get; private set; }

        /// <summary>
        /// 初始化一个<see cref="IndexInfo"/>类型的实例
        /// </summary>
        public IndexInfo()
        {
            ColumnRefIds = new List<string>();
        }
    }
}
