using System.Collections.Generic;
using Bing.Pdm.Models.Tables;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 包信息
    /// </summary>
    public class PackageInfo : PdmCommonInfo
    {
        /// <summary>
        /// 包信息列表
        /// </summary>
        [ChildObject("c:Packages", typeof(PackageInfo))]
        public List<PackageInfo> PackageInfos { get; set; }

        /// <summary>
        /// 表信息列表
        /// </summary>
        [ChildObject("c:Tables", typeof(TableInfo))]
        public List<TableInfo> TableInfos { get; set; }
    }
}
