using System;

namespace Bing.Pdm.Models.Others
{
    /// <summary>
    /// 目标模型信息
    /// </summary>
    public class TargetModelInfo : PdmCommonInfo
    {
        /// <summary>
        /// 目标模型标识
        /// </summary>
        public string TargetModelId { get; set; }

        /// <summary>
        /// 目标地址
        /// </summary>
        public string TargetUrl { get; set; }

        /// <summary>
        /// 目标标识
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// 目标类标识
        /// </summary>
        public string TargetClassId { get; set; }

        /// <summary>
        /// 目标最后修改时间
        /// </summary>
        public DateTime TargetLastModificationDate { get; set; }
    }
}
