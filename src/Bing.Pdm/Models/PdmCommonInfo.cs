using System;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// Pdm通用信息
    /// </summary>
    public abstract class PdmCommonInfo : ICreationAudited, IModifierAudited, IObjectId, IComment
    {
        /// <summary>
        /// 标识
        /// </summary>
        [NodeAttribute]
        public string Id { get; set; }

        /// <summary>
        /// 对象标识
        /// </summary>
        [NodeAlias("a:ObjectID")]
        public string ObjectId { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 代码
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
        /// 注释
        /// </summary>
        public string Comment { get; set; }
    }
}
