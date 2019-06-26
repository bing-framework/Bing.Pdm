using System;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 包信息
    /// </summary>
    public class PackageInfo
    {
        /// <summary>
        /// 包标识
        /// </summary>
        public string PackageId { get; set; }

        /// <summary>
        /// 对象标识
        /// </summary>
        public string ObjectId { get; set; }

        /// <summary>
        /// 包名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 包代码
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
        /// 包选项文本
        /// </summary>
        public string PackageOptionsText { get; set; }
    }
}
