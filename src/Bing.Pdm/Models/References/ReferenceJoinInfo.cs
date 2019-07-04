using System;

namespace Bing.Pdm.Models.References
{
    /// <summary>
    /// 引用关联信息
    /// </summary>
    public class ReferenceJoinInfo:IObjectId,ICreationAudited,IModifierAudited
    {
        /// <summary>
        /// 引用关联标识
        /// </summary>
        public string ReferenceJoinId { get; set; }

        /// <summary>
        /// 对象标识
        /// </summary>
        public string ObjectId { get; set; }

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
        /// 父表列标识
        /// </summary>
        public string ParentTableColumnId { get; set; }

        /// <summary>
        /// 子表列标识
        /// </summary>
        public string ChildTableColumnId { get; set; }

    }
}
