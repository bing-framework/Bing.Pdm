using System;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 架构信息
    /// </summary>
    public class SchemaInfo
    {
        /// <summary>
        /// 架构标识
        /// </summary>
        public string SchemaId { get; set; }

        /// <summary>
        /// 对象标识
        /// </summary>
        public string ObjectId { get; set; }

        /// <summary>
        /// 架构名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 架构代码 => 数据库中的架构名
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
        /// 声明类型
        /// </summary>
        public string StereoType { get; set; }
    }
}
