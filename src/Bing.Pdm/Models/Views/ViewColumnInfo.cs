using Newtonsoft.Json;

namespace Bing.Pdm.Models.Views
{
    /// <summary>
    /// 视图列信息。
    /// </summary>
    public class ViewColumnInfo : PdmCommonInfo, IComment, IDescription
    {
        /// <summary>
        /// 获取或设置所属视图标识。
        /// </summary>
        public string ViewId { get; set; }

        /// <summary>
        /// 获取或设置列描述。
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 获取或设置数据库类型。
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// 获取或设置数据长度。
        /// </summary>
        public string Length { get; set; }

        /// <summary>
        /// 获取或设置数据精度。
        /// </summary>
        public string Precision { get; set; }
        /// <summary>
        /// 获取或设置默认值。
        /// </summary>
        public string DefaultValue { get; set; }
        /// <summary>
        /// 获取或设置列是否必填。
        /// </summary>
        public bool Mandatory { get; set; }

        /// <summary>
        /// 初始化一个 <see cref="ViewColumnInfo"/> 类型的实例。
        /// </summary>
        public ViewColumnInfo() { }
    }
}
