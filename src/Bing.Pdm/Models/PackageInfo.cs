using System.Collections.Generic;
using System.Linq;
using Bing.Pdm.Models.PhysicalDiagrams;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Models.Views;
using Newtonsoft.Json;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 包及其子对象信息。
    /// </summary>
    public sealed class PackageInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取嵌套包集合。
        /// </summary>
        public List<PackageInfo> Packages { get; } = new List<PackageInfo>();

        /// <summary>
        /// 获取包中的表集合。
        /// </summary>
        public List<TableInfo> Tables { get; } = new List<TableInfo>();

        /// <summary>
        /// 获取包中的 Shortcut 集合。
        /// </summary>
        public List<PdmShortcutInfo> Shortcuts { get; } = new List<PdmShortcutInfo>();

        /// <summary>
        /// 获取包中的视图集合。
        /// </summary>
        public List<ViewInfo> Views { get; } = new List<ViewInfo>();

        /// <summary>
        /// 获取包中的引用集合。
        /// </summary>
        public List<ReferenceInfo> References { get; } = new List<ReferenceInfo>();

        /// <summary>
        /// 获取包中的物理图集合。
        /// </summary>
        public List<PhysicalDiagramInfo> PhysicalDiagrams { get; } = new List<PhysicalDiagramInfo>();

        /// <summary>
        /// 获取兼容名称的嵌套包集合。
        /// </summary>
        [JsonIgnore]
        public List<PackageInfo> PackageInfos => Packages;

        /// <summary>
        /// 获取兼容名称的表集合。
        /// </summary>
        [JsonIgnore]
        public List<TableInfo> TableInfos => Tables;

        /// <summary>
        /// 获取包及所有嵌套包中的表。
        /// </summary>
        [JsonIgnore]
        public IEnumerable<TableInfo> AllTables => Tables.Concat(Packages.SelectMany(x => x.AllTables));

        /// <summary>
        /// 获取包及所有嵌套包中的 Shortcut。
        /// </summary>
        [JsonIgnore]
        public IEnumerable<PdmShortcutInfo> AllShortcuts => Shortcuts.Concat(Packages.SelectMany(x => x.AllShortcuts));

        /// <summary>
        /// 获取包及所有嵌套包中的引用。
        /// </summary>
        [JsonIgnore]
        public IEnumerable<ReferenceInfo> AllReferences => References.Concat(Packages.SelectMany(x => x.AllReferences));

        /// <summary>
        /// 获取包及所有嵌套包中的视图。
        /// </summary>
        [JsonIgnore]
        public IEnumerable<ViewInfo> AllViews => Views.Concat(Packages.SelectMany(x => x.AllViews));

        /// <summary>
        /// 获取包及所有嵌套包中的物理图。
        /// </summary>
        [JsonIgnore]
        public IEnumerable<PhysicalDiagramInfo> AllPhysicalDiagrams => PhysicalDiagrams.Concat(Packages.SelectMany(x => x.AllPhysicalDiagrams));
    }
}
