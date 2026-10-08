using System.Collections.Generic;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// PowerDesigner 复制关系。
    /// </summary>
    public sealed class PdmReplicationInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置来源对象 GUID。
        /// </summary>
        public string OriginalId { get; set; }
        /// <summary>
        /// 获取或设置解析后的来源对象地址。
        /// </summary>
        public PdmObjectAddress OriginalAddress { get; set; }
        /// <summary>
        /// 获取或设置来源类型 GUID。
        /// </summary>
        public string OriginalClassId { get; set; }
        /// <summary>
        /// 获取或设置副本对象的原始 Ref。
        /// </summary>
        public string ReplicaObjectRef { get; set; }
        /// <summary>
        /// 获取或设置解析后的副本对象地址。
        /// </summary>
        public PdmObjectAddress ReplicaObjectAddress { get; set; }
        /// <summary>
        /// 获取或设置副本对象类型。
        /// </summary>
        public string ReplicaObjectKind { get; set; }
        /// <summary>
        /// 获取子复制关系的原始引用。
        /// </summary>
        public List<string> SubReplicationRefs { get; } = new List<string>();
        /// <summary>
        /// 获取复制关系包含的子复制关系。
        /// </summary>
        public List<PdmSubReplicationInfo> SubReplications { get; } = new List<PdmSubReplicationInfo>();
        /// <summary>
        /// 获取尚未建模的原始属性。
        /// </summary>
        public Dictionary<string, string> RawAttributes { get; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// PowerDesigner 子对象复制关系。
    /// </summary>
    public sealed class PdmSubReplicationInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置父复制关系的 PDM ID。
        /// </summary>
        public string ParentReplicationId { get; set; }
        /// <summary>
        /// 获取或设置来源对象 GUID。
        /// </summary>
        public string OriginalId { get; set; }
        /// <summary>
        /// 获取或设置解析后的来源对象地址。
        /// </summary>
        public PdmObjectAddress OriginalAddress { get; set; }
        /// <summary>
        /// 获取或设置来源类型 GUID。
        /// </summary>
        public string OriginalClassId { get; set; }
        /// <summary>
        /// 获取或设置副本对象的原始 Ref。
        /// </summary>
        public string ReplicaObjectRef { get; set; }
        /// <summary>
        /// 获取或设置解析后的副本对象地址。
        /// </summary>
        public PdmObjectAddress ReplicaObjectAddress { get; set; }
        /// <summary>
        /// 获取或设置副本对象类型。
        /// </summary>
        public string ReplicaObjectKind { get; set; }
        /// <summary>
        /// 获取尚未建模的原始属性。
        /// </summary>
        public Dictionary<string, string> RawAttributes { get; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// 嵌入的快捷模型对象描述。
    /// </summary>
    public sealed class PdmEmbeddedObjectInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置 XML 对象类型。
        /// </summary>
        public string Kind { get; set; }
        /// <summary>
        /// 获取或设置所属嵌入对象的 PDM ID。
        /// </summary>
        public string ParentId { get; set; }
        /// <summary>
        /// 获取或设置快捷对象的目标 GUID。
        /// </summary>
        public string TargetId { get; set; }
        /// <summary>
        /// 获取尚未建模的原始属性。
        /// </summary>
        public Dictionary<string, string> RawAttributes { get; } = new Dictionary<string, string>();
    }
}
