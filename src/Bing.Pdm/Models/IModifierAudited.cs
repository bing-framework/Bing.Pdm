using System;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 修改人审计
    /// </summary>
    public interface IModifierAudited
    {
        /// <summary>
        /// 修改日期
        /// </summary>
        DateTime ModificationDate { get; set; }

        /// <summary>
        /// 修改人
        /// </summary>
        string Modifier { get; set; }
    }
}
