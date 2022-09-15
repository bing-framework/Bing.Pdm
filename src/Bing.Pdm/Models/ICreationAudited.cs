using System;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 创建人审计
    /// </summary>
    public interface ICreationAudited
    {
        /// <summary>
        /// 创建日期
        /// </summary>
        DateTime CreationDate { get; set; }

        /// <summary>
        /// 创建人
        /// </summary>
        string Creator { get; set; }
    }
}
