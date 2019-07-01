using Newtonsoft.Json;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 视图列信息
    /// </summary>
    public class ViewColumnInfo : PdmCommonInfo
    {
        /// <summary>
        /// 视图列标识
        /// </summary>
        public string ViewColumnId { get; set; }

        /// <summary>
        /// 注释
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// 数据长度
        /// </summary>
        public string Length { get; set; }

        /// <summary>
        /// 所有者视图信息
        /// </summary>
        [JsonIgnore]
        public ViewInfo OwnerViewInfo { get; private set; }

        /// <summary>
        /// 初始化一个<see cref="ViewColumnInfo"/>类型的实例
        /// </summary>
        /// <param name="view">所有者视图信息</param>
        public ViewColumnInfo(ViewInfo view)
        {
            OwnerViewInfo = view;
        }
    }
}
