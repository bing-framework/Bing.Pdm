using System;
using System.Collections.Generic;
using Bing.Pdm.Models.Keys;
using Bing.Pdm.Models.PhysicalDiagrams;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Models.Views;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 为 PDM 模型提供按标识查找的索引。
    /// </summary>
    public sealed class PdmLookupIndex
    {
        /// <summary>
        /// 按节点标识查找表对象。
        /// </summary>
        private readonly Dictionary<string, TableInfo> _tables = new Dictionary<string, TableInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按节点标识查找包对象。
        /// </summary>
        private readonly Dictionary<string, PackageInfo> _packages = new Dictionary<string, PackageInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按节点标识查找视图对象。
        /// </summary>
        private readonly Dictionary<string, ViewInfo> _views = new Dictionary<string, ViewInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按节点标识查找引用对象。
        /// </summary>
        private readonly Dictionary<string, ReferenceInfo> _references = new Dictionary<string, ReferenceInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按节点标识查找表列对象。
        /// </summary>
        private readonly Dictionary<string, ColumnInfo> _columns = new Dictionary<string, ColumnInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按节点标识查找视图列对象。
        /// </summary>
        private readonly Dictionary<string, ViewColumnInfo> _viewColumns = new Dictionary<string, ViewColumnInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按节点标识查找表键对象。
        /// </summary>
        private readonly Dictionary<string, KeyInfo> _keys = new Dictionary<string, KeyInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按节点标识查找所有者对象。
        /// </summary>
        private readonly Dictionary<string, PdmOwnerInfo> _owners = new Dictionary<string, PdmOwnerInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按节点标识查找物理图对象。
        /// </summary>
        private readonly Dictionary<string, PhysicalDiagramInfo> _diagrams = new Dictionary<string, PhysicalDiagramInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按节点标识查找物理图符号。
        /// </summary>
        private readonly Dictionary<string, DiagramSymbolInfo> _symbols = new Dictionary<string, DiagramSymbolInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按节点标识查找 Shortcut 对象。
        /// </summary>
        private readonly Dictionary<string, PdmShortcutInfo> _shortcuts = new Dictionary<string, PdmShortcutInfo>(StringComparer.Ordinal);

        /// <summary>
        /// 初始化一个 <see cref="PdmLookupIndex"/> 类型的实例。
        /// </summary>
        /// <param name="model">待索引的 PDM 模型。</param>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> 为 <see langword="null"/>。</exception>
        public PdmLookupIndex(PdmInfo model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            Add(model.Owners, _owners);
            Add(model.Shortcuts, _shortcuts);
            AddPackages(model.Packages);
            AddTables(model.Tables);
            AddViews(model.Views);
            AddReferences(model.References);
            AddDiagrams(model.PhysicalDiagrams);
        }

        /// <summary>
        /// 按标识查找表。
        /// </summary>
        /// <param name="id">表标识；为 <see langword="null"/> 时按空字符串查找。</param>
        /// <param name="value">找到的表信息；未找到时为默认值。</param>
        /// <returns>找到表时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool TryGetTable(string id, out TableInfo value) => _tables.TryGetValue(id ?? string.Empty, out value);
        /// <summary>
        /// 按标识查找包。
        /// </summary>
        /// <param name="id">包标识；为 <see langword="null"/> 时按空字符串查找。</param>
        /// <param name="value">找到的包信息；未找到时为默认值。</param>
        /// <returns>找到包时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool TryGetPackage(string id, out PackageInfo value) => _packages.TryGetValue(id ?? string.Empty, out value);
        /// <summary>
        /// 按标识查找视图。
        /// </summary>
        /// <param name="id">视图标识；为 <see langword="null"/> 时按空字符串查找。</param>
        /// <param name="value">找到的视图信息；未找到时为默认值。</param>
        /// <returns>找到视图时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool TryGetView(string id, out ViewInfo value) => _views.TryGetValue(id ?? string.Empty, out value);
        /// <summary>
        /// 按标识查找引用。
        /// </summary>
        /// <param name="id">引用标识；为 <see langword="null"/> 时按空字符串查找。</param>
        /// <param name="value">找到的引用信息；未找到时为默认值。</param>
        /// <returns>找到引用时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool TryGetReference(string id, out ReferenceInfo value) => _references.TryGetValue(id ?? string.Empty, out value);
        /// <summary>
        /// 按标识查找表列。
        /// </summary>
        /// <param name="id">列标识；为 <see langword="null"/> 时按空字符串查找。</param>
        /// <param name="value">找到的列信息；未找到时为默认值。</param>
        /// <returns>找到列时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool TryGetColumn(string id, out ColumnInfo value) => _columns.TryGetValue(id ?? string.Empty, out value);
        /// <summary>
        /// 按标识查找视图列。
        /// </summary>
        /// <param name="id">视图列标识；为 <see langword="null"/> 时按空字符串查找。</param>
        /// <param name="value">找到的视图列信息；未找到时为默认值。</param>
        /// <returns>找到视图列时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool TryGetViewColumn(string id, out ViewColumnInfo value) => _viewColumns.TryGetValue(id ?? string.Empty, out value);
        /// <summary>
        /// 按标识查找键。
        /// </summary>
        /// <param name="id">键标识；为 <see langword="null"/> 时按空字符串查找。</param>
        /// <param name="value">找到的键信息；未找到时为默认值。</param>
        /// <returns>找到键时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool TryGetKey(string id, out KeyInfo value) => _keys.TryGetValue(id ?? string.Empty, out value);
        /// <summary>
        /// 按标识查找所有者。
        /// </summary>
        /// <param name="id">所有者标识；为 <see langword="null"/> 时按空字符串查找。</param>
        /// <param name="value">找到的所有者信息；未找到时为默认值。</param>
        /// <returns>找到所有者时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool TryGetOwner(string id, out PdmOwnerInfo value) => _owners.TryGetValue(id ?? string.Empty, out value);
        /// <summary>
        /// 按标识查找物理图。
        /// </summary>
        /// <param name="id">物理图标识；为 <see langword="null"/> 时按空字符串查找。</param>
        /// <param name="value">找到的物理图信息；未找到时为默认值。</param>
        /// <returns>找到物理图时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool TryGetDiagram(string id, out PhysicalDiagramInfo value) => _diagrams.TryGetValue(id ?? string.Empty, out value);
        /// <summary>
        /// 按标识查找图形符号。
        /// </summary>
        /// <param name="id">图形符号标识；为 <see langword="null"/> 时按空字符串查找。</param>
        /// <param name="value">找到的图形符号；未找到时为默认值。</param>
        /// <returns>找到图形符号时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool TryGetSymbol(string id, out DiagramSymbolInfo value) => _symbols.TryGetValue(id ?? string.Empty, out value);
        /// <summary>
        /// 按标识查找 Shortcut。
        /// </summary>
        /// <param name="id">Shortcut 标识；为 <see langword="null"/> 时按空字符串查找。</param>
        /// <param name="value">找到的 Shortcut 信息；未找到时为默认值。</param>
        /// <returns>找到 Shortcut 时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool TryGetShortcut(string id, out PdmShortcutInfo value) => _shortcuts.TryGetValue(id ?? string.Empty, out value);

        /// <summary>
        /// 递归添加包及其对象到索引。
        /// </summary>
        /// <param name="packages">待索引的包集合。</param>
        private void AddPackages(IEnumerable<PackageInfo> packages)
        {
            foreach (var package in packages)
            {
                Add(package, _packages);
                Add(package.Shortcuts, _shortcuts);
                AddTables(package.Tables);
                AddViews(package.Views);
                AddReferences(package.References);
                AddDiagrams(package.PhysicalDiagrams);
                AddPackages(package.Packages);
            }
        }

        /// <summary>
        /// 添加表及其列和键到索引。
        /// </summary>
        /// <param name="tables">待索引的表集合。</param>
        private void AddTables(IEnumerable<TableInfo> tables)
        {
            foreach (var table in tables)
            {
                Add(table, _tables);
                Add(table.Columns, _columns);
                Add(table.Keys, _keys);
            }
        }

        /// <summary>
        /// 添加视图及其列到索引。
        /// </summary>
        /// <param name="views">待索引的视图集合。</param>
        private void AddViews(IEnumerable<ViewInfo> views)
        {
            foreach (var view in views)
            {
                Add(view, _views);
                Add(view.Columns, _viewColumns);
            }
        }

        /// <summary>
        /// 添加引用到索引。
        /// </summary>
        /// <param name="references">待索引的引用集合。</param>
        private void AddReferences(IEnumerable<ReferenceInfo> references)
        {
            foreach (var reference in references) Add(reference, _references);
        }

        /// <summary>
        /// 添加物理图及其符号到索引。
        /// </summary>
        /// <param name="diagrams">待索引的物理图集合。</param>
        private void AddDiagrams(IEnumerable<PhysicalDiagramInfo> diagrams)
        {
            foreach (var diagram in diagrams)
            {
                Add(diagram, _diagrams);
                foreach (var symbol in diagram.AllSymbols)
                    if (symbol != null && !string.IsNullOrEmpty(symbol.Id) && !_symbols.ContainsKey(symbol.Id))
                        _symbols.Add(symbol.Id, symbol);
            }
        }

        /// <summary>
        /// 将对象集合添加到索引。
        /// </summary>
        /// <param name="values">待索引的对象集合。</param>
        /// <param name="index">目标索引。</param>
        private static void Add<T>(IEnumerable<T> values, Dictionary<string, T> index) where T : PdmCommonInfo
        {
            foreach (var value in values) Add(value, index);
        }

        /// <summary>
        /// 将对象添加到索引。
        /// </summary>
        /// <param name="value">待索引的对象。</param>
        /// <param name="index">目标索引。</param>
        private static void Add<T>(T value, Dictionary<string, T> index) where T : PdmCommonInfo
        {
            if (value != null && !string.IsNullOrEmpty(value.Id) && !index.ContainsKey(value.Id))
                index.Add(value.Id, value);
        }
    }
}
