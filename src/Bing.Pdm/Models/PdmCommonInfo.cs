using System;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 通用信息。
    /// </summary>
    public abstract class PdmCommonInfo : ICreationAudited, IModifierAudited, IObjectId, IComment
    {
        /// <summary>
        /// 获取或设置 PDM 节点标识。
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 获取或设置 PowerDesigner 对象标识。
        /// </summary>
        public string ObjectId { get; set; }

        /// <summary>
        /// 获取或设置对象名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置对象代码。
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 获取或设置对象创建时间。
        /// </summary>
        public DateTime CreationDate { get; set; }

        /// <summary>
        /// 获取或设置对象创建者。
        /// </summary>
        public string Creator { get; set; }

        /// <summary>
        /// 获取或设置对象最后修改时间。
        /// </summary>
        public DateTime ModificationDate { get; set; }

        /// <summary>
        /// 获取或设置对象最后修改者。
        /// </summary>
        public string Modifier { get; set; }

        /// <summary>
        /// 获取或设置对象注释。
        /// </summary>
        public string Comment { get; set; }
    }
}
