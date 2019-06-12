namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 引用关联
    /// </summary>
    public class PdmReferenceJoin
    {
        /// <summary>
        /// 系统编号
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 父表列
        /// </summary>
        public PdmColumn ParentTableColumn { get; set; }

        /// <summary>
        /// 子表列
        /// </summary>
        public PdmColumn ChildTableColumn { get; set; }
    }
}
