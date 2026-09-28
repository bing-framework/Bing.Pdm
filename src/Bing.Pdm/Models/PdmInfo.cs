using System.Collections.Generic;
using System.Linq;
using Bing.Pdm.Models.PhysicalDiagrams;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Models.Views;
using Bing.Pdm.Models.Others;
using Newtonsoft.Json;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 模型信息。
    /// </summary>
    public sealed class PdmInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置模型标识。
        /// </summary>
        [JsonIgnore]
        public string PdmId { get => Id; set => Id = value; }
        /// <summary>
        /// 获取或设置包选项原文。
        /// </summary>
        [JsonIgnore]
        public string PackageOptionsText { get; set; }
        /// <summary>
        /// 获取或设置模型选项原文。
        /// </summary>
        [JsonIgnore]
        public string ModelOptionsText { get; set; }
        /// <summary>
        /// 获取或设置作者。
        /// </summary>
        public string Author { get; set; }
        /// <summary>
        /// 获取或设置版本。
        /// </summary>
        public string Version { get; set; }
        /// <summary>
        /// 获取或设置仓库文件名。
        /// </summary>
        public string RepositoryFileName { get; set; }
        /// <summary>
        /// 获取或设置数据库管理系统名称。
        /// </summary>
        public string DbmsName { get; set; }
        /// <summary>
        /// 获取或设置数据库管理系统代码。
        /// </summary>
        public string DbmsCode { get; set; }
        /// <summary>
        /// 获取或设置数据库管理系统信息。
        /// </summary>
        [JsonIgnore]
        public DbmsInfo Dbms { get; set; }
        /// <summary>
        /// 获取所有者集合。
        /// </summary>
        public List<PdmOwnerInfo> Owners { get; } = new List<PdmOwnerInfo>();
        /// <summary>
        /// 获取模式集合。
        /// </summary>
        public List<SchemaInfo> Schemas { get; } = new List<SchemaInfo>();
        /// <summary>
        /// 获取顶层包集合。
        /// </summary>
        public List<PackageInfo> Packages { get; } = new List<PackageInfo>();
        /// <summary>
        /// 获取顶层表集合。
        /// </summary>
        public List<TableInfo> Tables { get; } = new List<TableInfo>();
        /// <summary>
        /// 获取顶层 Shortcut 集合。
        /// </summary>
        public List<PdmShortcutInfo> Shortcuts { get; } = new List<PdmShortcutInfo>();
        /// <summary>
        /// 获取顶层视图集合。
        /// </summary>
        public List<ViewInfo> Views { get; } = new List<ViewInfo>();
        /// <summary>
        /// 获取顶层引用集合。
        /// </summary>
        public List<ReferenceInfo> References { get; } = new List<ReferenceInfo>();
        /// <summary>
        /// 获取顶层物理图集合。
        /// </summary>
        public List<PhysicalDiagramInfo> PhysicalDiagrams { get; } = new List<PhysicalDiagramInfo>();
        /// <summary>
        /// 获取读取诊断集合。
        /// </summary>
        public List<PdmDiagnostic> Diagnostics { get; } = new List<PdmDiagnostic>();
        /// <summary>
        /// 获取兼容名称的包集合。
        /// </summary>
        [JsonIgnore]
        public List<PackageInfo> PackageInfos => Packages;
        /// <summary>
        /// 获取兼容名称的表集合。
        /// </summary>
        [JsonIgnore]
        public List<TableInfo> TableInfos => Tables;
        /// <summary>
        /// 获取兼容名称的引用集合。
        /// </summary>
        [JsonIgnore]
        public List<ReferenceInfo> ReferenceInfos => References;
        /// <summary>
        /// 获取默认分组集合。
        /// </summary>
        [JsonIgnore]
        public List<GroupInfo> DefaultGroups { get; } = new List<GroupInfo>();
        /// <summary>
        /// 获取目标模型集合。
        /// </summary>
        [JsonIgnore]
        public List<TargetModelInfo> TargetModels { get; } = new List<TargetModelInfo>();

        /// <summary>
        /// 按标识查找模式信息。
        /// </summary>
        /// <param name="schemaId">模式标识。</param>
        /// <returns>匹配的模式信息。</returns>
        /// <exception cref="System.ArgumentException">未找到指定标识的模式。</exception>
        public SchemaInfo FindSchema(string schemaId)
        {
            var result = Schemas.FirstOrDefault(x => x.SchemaId == schemaId);
            if (result == null) throw new System.ArgumentException(schemaId + " Schema Not Found.");
            return result;
        }

        /// <summary>
        /// 获取模型对象索引。
        /// </summary>
        [JsonIgnore]
        public PdmLookupIndex Lookup { get; internal set; }

        /// <summary>
        /// 获取模型中的全部表。
        /// </summary>
        [JsonIgnore]
        public IEnumerable<TableInfo> AllTables => Tables.Concat(Packages.SelectMany(x => x.AllTables));

        /// <summary>
        /// 获取模型中的全部 Shortcut。
        /// </summary>
        [JsonIgnore]
        public IEnumerable<PdmShortcutInfo> AllShortcuts => Shortcuts.Concat(Packages.SelectMany(x => x.AllShortcuts));

        /// <summary>
        /// 获取模型中的全部引用。
        /// </summary>
        [JsonIgnore]
        public IEnumerable<ReferenceInfo> AllReferences => References.Concat(Packages.SelectMany(x => x.AllReferences));

        /// <summary>
        /// 获取模型中的全部视图。
        /// </summary>
        [JsonIgnore]
        public IEnumerable<ViewInfo> AllViews => Views.Concat(Packages.SelectMany(x => x.AllViews));

        /// <summary>
        /// 获取模型中的全部物理图。
        /// </summary>
        [JsonIgnore]
        public IEnumerable<PhysicalDiagramInfo> AllPhysicalDiagrams => PhysicalDiagrams.Concat(Packages.SelectMany(x => x.AllPhysicalDiagrams));
    }

    /// <summary>
    /// PDM 读取诊断信息。
    /// </summary>
    public sealed class PdmDiagnostic
    {
        /// <summary>
        /// 获取或设置诊断代码。
        /// </summary>
        public string Code { get; set; }
        /// <summary>
        /// 获取或设置诊断来源标识。
        /// </summary>
        public string SourceId { get; set; }
        /// <summary>
        /// 获取或设置诊断消息。
        /// </summary>
        public string Message { get; set; }
    }
}
