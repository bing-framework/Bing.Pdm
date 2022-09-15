using System.Collections.Generic;

namespace Bing.Pdm.Models.Views
{
    /// <summary>
    /// 视图信息
    /// </summary>
    public class ViewInfo : PdmCommonInfo
    {
        /// <summary>
        /// 视图标识
        /// </summary>
        public string ViewId { get; set; }

        /// <summary>
        /// 视图SQL
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public string ViewSQLQuery { get; set; }

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
