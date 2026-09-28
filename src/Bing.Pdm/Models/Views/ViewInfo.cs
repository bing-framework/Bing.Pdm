using System.Collections.Generic;

namespace Bing.Pdm.Models.Views
{
    /// <summary>
    /// 视图信息。
    /// </summary>
    public class ViewInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置所属包标识。
        /// </summary>
        public string PackageId { get; set; }

        /// <summary>
        /// 获取或设置视图 SQL 查询。
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public string ViewSQLQuery { get; set; }

        /// <summary>
        /// 获取或设置视图描述。
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 获取或设置带标签的 SQL 查询。
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public string TaggedSQLQuery { get; set; }

        /// <summary>
        /// 获取视图列集合。
        /// </summary>
        public List<ViewColumnInfo> Columns { get; private set; }

        /// <summary>
        /// 初始化一个 <see cref="ViewInfo"/> 类型的实例。
        /// </summary>
        public ViewInfo()
        {
            Columns = new List<ViewColumnInfo>();
        }
    }
}
