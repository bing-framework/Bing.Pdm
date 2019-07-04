namespace Bing.Pdm.Models
{
    /// <summary>
    /// DBMS 信息
    /// </summary>
    public class DbmsInfo : PdmCommonInfo
    {
        /// <summary>
        /// DBMS标识
        /// </summary>
        public string DbmsId { get; set; }

        /// <summary>
        /// 目标标识
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// 目标类标识
        /// </summary>
        public string TargetClassId { get; set; }
    }
}
