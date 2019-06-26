using System;
using System.Collections.Generic;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 视图信息
    /// </summary>
    public class ViewInfo
    {
        /// <summary>
        /// 视图标识
        /// </summary>
        public string ViewId { get; set; }

        /// <summary>
        /// 对象标识
        /// </summary>
        public string ObjectId { get; set; }

        /// <summary>
        /// 视图名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 视图代码 => 数据库中的视图名
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
        /// 视图SQL
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public string ViewSQLQuery { get; set; }

        /// <summary>
        /// 注释
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 标签化的SQL查询
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public string TaggedSQLQuery { get; set; }

        /// <summary>
        /// 视图列集合
        /// </summary>
        public List<ViewColumnInfo> Columns { get; private set; }

        /// <summary>
        /// 初始化一个<see cref="ViewInfo"/>类型的实例
        /// </summary>
        public ViewInfo()
        {
            Columns = new List<ViewColumnInfo>();
        }
    }
}
