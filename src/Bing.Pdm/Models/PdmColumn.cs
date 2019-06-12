namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 列结构
    /// </summary>
    public class PdmColumn
    {
        /// <summary>
        /// 系统编号
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// 数据长度
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// 精度
        /// </summary>
        public int Precision { get; set; }

        /// <summary>
        /// 约束
        /// </summary>
        public int Mandatory { get; set; } = 0;

        /// <summary>
        /// 默认值
        /// </summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// 最小值
        /// </summary>
        public string LowValue { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public string HighValue { get; set; }

        /// <summary>
        /// 注释
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// PDM 表结构
        /// </summary>
        public PdmTable Table { get; set; }
    }
}
