using System;
using System.Xml;
using Bing.Pdm.Models.Keys;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Models.Views;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 数据库管理系统信息。
    /// </summary>
    /// <remarks>
    /// 保留旧版加载器 API 使用的属性。
    /// </remarks>
    public class DbmsInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置数据库管理系统标识。
        /// </summary>
        public string DbmsId { get; set; }
        /// <summary>
        /// 获取或设置目标标识。
        /// </summary>
        public string TargetId { get; set; }
        /// <summary>
        /// 获取或设置目标类型标识。
        /// </summary>
        public string TargetClassId { get; set; }
    }
}

namespace Bing.Pdm.Models.Others
{
    /// <summary>
    /// 默认分组信息。
    /// </summary>
    /// <remarks>
    /// 保留旧版加载器 API 使用的属性。
    /// </remarks>
    public class GroupInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置分组标识。
        /// </summary>
        public string GroupId { get; set; }
    }

    /// <summary>
    /// 目标模型信息。
    /// </summary>
    /// <remarks>
    /// 保留旧版加载器 API 使用的属性。
    /// </remarks>
    public class TargetModelInfo : PdmCommonInfo
    {
        /// <summary>
        /// 获取或设置目标模型标识。
        /// </summary>
        public string TargetModelId { get; set; }
        /// <summary>
        /// 获取或设置目标地址。
        /// </summary>
        public string TargetUrl { get; set; }
        /// <summary>
        /// 获取或设置目标标识。
        /// </summary>
        public string TargetId { get; set; }
        /// <summary>
        /// 获取或设置目标类型标识。
        /// </summary>
        public string TargetClassId { get; set; }
        /// <summary>
        /// 获取或设置目标最后修改时间。
        /// </summary>
        public DateTime TargetLastModificationDate { get; set; }
    }
}

namespace Bing.Pdm.Models.References
{
    /// <summary>
    /// 对象引用信息。
    /// </summary>
    /// <remarks>
    /// 保留旧版加载器 API 使用的引用属性。
    /// </remarks>
    public class RefInfo
    {
        /// <summary>
        /// 获取或设置原始对象引用标识。
        /// </summary>
        public string Ref { get; set; }
    }
}

namespace Bing.Pdm.Models.PhysicalDiagrams
{
    /// <summary>
    /// 包图形符号信息。
    /// </summary>
    /// <remarks>
    /// 保留旧版加载器 API 的类型定义。
    /// </remarks>
    public class PackageSymbolInfo { }

    /// <summary>
    /// 表图形符号信息。
    /// </summary>
    /// <remarks>
    /// 保留旧版加载器 API 使用的图形属性。
    /// </remarks>
    public class TableSymbolInfo
    {
        /// <summary>
        /// 获取或设置表符号标识。
        /// </summary>
        public string TableSymbolId { get; set; }
        /// <summary>
        /// 获取或设置创建时间。
        /// </summary>
        public DateTime CreationDate { get; set; }
        /// <summary>
        /// 获取或设置修改时间。
        /// </summary>
        public DateTime ModificationDate { get; set; }
        /// <summary>
        /// 获取或设置图形范围。
        /// </summary>
        public string Rect { get; set; }
        /// <summary>
        /// 获取或设置线条颜色。
        /// </summary>
        public string LineColor { get; set; }
        /// <summary>
        /// 获取或设置填充颜色。
        /// </summary>
        public string FillColor { get; set; }
        /// <summary>
        /// 获取或设置阴影颜色。
        /// </summary>
        public string ShadowColor { get; set; }
        /// <summary>
        /// 获取或设置字体列表。
        /// </summary>
        public string FontList { get; set; }
        /// <summary>
        /// 获取或设置画刷样式。
        /// </summary>
        public string BrushStyle { get; set; }
        /// <summary>
        /// 获取或设置渐变填充模式。
        /// </summary>
        public string GradientFillMode { get; set; }
        /// <summary>
        /// 获取或设置渐变结束颜色。
        /// </summary>
        public string GradientEndColor { get; set; }
        /// <summary>
        /// 获取或设置表标识。
        /// </summary>
        public string TableId { get; set; }
    }

    /// <summary>
    /// 引用图形符号信息。
    /// </summary>
    /// <remarks>
    /// 保留旧版加载器 API 使用的图形属性。
    /// </remarks>
    public class ReferenceSymbolInfo
    {
        /// <summary>
        /// 获取或设置引用符号标识。
        /// </summary>
        public string ReferenceSymbolId { get; set; }
        /// <summary>
        /// 获取或设置创建时间。
        /// </summary>
        public DateTime CreationDate { get; set; }
        /// <summary>
        /// 获取或设置修改时间。
        /// </summary>
        public DateTime ModificationDate { get; set; }
        /// <summary>
        /// 获取或设置图形范围。
        /// </summary>
        public string Rect { get; set; }
        /// <summary>
        /// 获取或设置线条颜色。
        /// </summary>
        public string LineColor { get; set; }
        /// <summary>
        /// 获取或设置阴影颜色。
        /// </summary>
        public string ShadowColor { get; set; }
        /// <summary>
        /// 获取或设置引用标识。
        /// </summary>
        public string ReferenceId { get; set; }
        /// <summary>
        /// 获取或设置起点符号标识。
        /// </summary>
        public string SourceSymbolId { get; set; }
        /// <summary>
        /// 获取或设置终点符号标识。
        /// </summary>
        public string DestinationSymbolId { get; set; }
        /// <summary>
        /// 获取或设置折线点列表。
        /// </summary>
        public string ListOfPoints { get; set; }
    }
}

namespace Bing.Pdm.Abstractions.Loaders
{
    /// <summary>
    /// 读取数据库管理系统信息。
    /// </summary>
    public interface IDbmsLoader
    {
        /// <summary>
        /// 读取数据库管理系统节点。
        /// </summary>
        /// <param name="node">数据库管理系统 XML 节点。</param>
        /// <returns>解析后的数据库管理系统信息。</returns>
        Bing.Pdm.Models.DbmsInfo GetDbms(XmlNode node);
    }
    /// <summary>
    /// 读取默认分组信息。
    /// </summary>
    public interface IGroupLoader
    {
        /// <summary>
        /// 读取分组节点。
        /// </summary>
        /// <param name="node">分组 XML 节点。</param>
        /// <returns>解析后的默认分组信息。</returns>
        Bing.Pdm.Models.Others.GroupInfo GetGroup(XmlNode node);
    }
    /// <summary>
    /// 读取表键信息。
    /// </summary>
    public interface IKeyLoader
    {
        /// <summary>
        /// 读取键节点。
        /// </summary>
        /// <param name="node">表键 XML 节点。</param>
        /// <param name="ownerTable">键所属的表。</param>
        /// <returns>解析后的键信息。</returns>
        KeyInfo GetKey(XmlNode node, TableInfo ownerTable);
    }
    /// <summary>
    /// 读取包信息。
    /// </summary>
    public interface IPackageLoader
    {
        /// <summary>
        /// 读取包节点。
        /// </summary>
        /// <param name="node">包 XML 节点。</param>
        /// <returns>解析后的包信息。</returns>
        Bing.Pdm.Models.PackageInfo GetPackage(XmlNode node);
    }
    /// <summary>
    /// 读取 PDM 模型。
    /// </summary>
    public interface IPdmLoader
    {
        /// <summary>
        /// 按路径读取 PDM 文件。
        /// </summary>
        /// <param name="filePath">PDM 文件路径。</param>
        /// <returns>解析后的 PDM 模型。</returns>
        Bing.Pdm.Models.PdmInfo GetPdm(string filePath);
    }
    /// <summary>
    /// 读取物理图信息。
    /// </summary>
    public interface IPhysicalDiagramLoader
    {
        /// <summary>
        /// 读取物理图节点。
        /// </summary>
        /// <param name="node">物理图 XML 节点。</param>
        /// <returns>解析后的物理图信息。</returns>
        Bing.Pdm.Models.PhysicalDiagrams.PhysicalDiagramInfo GetPhysicalDiagram(XmlNode node);
    }
    /// <summary>
    /// 读取引用信息。
    /// </summary>
    public interface IReferenceLoader
    {
        /// <summary>
        /// 读取引用节点。
        /// </summary>
        /// <param name="node">引用 XML 节点。</param>
        /// <returns>解析后的引用信息。</returns>
        Bing.Pdm.Models.References.ReferenceInfo GetReference(XmlNode node);
    }
    /// <summary>
    /// 读取模式信息。
    /// </summary>
    public interface ISchemaLoader
    {
        /// <summary>
        /// 读取模式节点。
        /// </summary>
        /// <param name="node">模式 XML 节点。</param>
        /// <returns>解析后的模式信息。</returns>
        Bing.Pdm.Models.SchemaInfo GetSchema(XmlNode node);
    }
    /// <summary>
    /// 读取表信息。
    /// </summary>
    public interface ITableLoader
    {
        /// <summary>
        /// 读取表节点。
        /// </summary>
        /// <param name="node">表 XML 节点。</param>
        /// <returns>解析后的表信息。</returns>
        TableInfo GetTable(XmlNode node);
    }
    /// <summary>
    /// 读取目标模型信息。
    /// </summary>
    public interface ITargetModelLoader
    {
        /// <summary>
        /// 读取目标模型节点。
        /// </summary>
        /// <param name="node">目标模型 XML 节点。</param>
        /// <returns>解析后的目标模型信息。</returns>
        Bing.Pdm.Models.Others.TargetModelInfo GetTargetModel(XmlNode node);
    }
    /// <summary>
    /// 读取视图信息。
    /// </summary>
    public interface IViewLoader
    {
        /// <summary>
        /// 读取视图节点。
        /// </summary>
        /// <param name="node">视图 XML 节点。</param>
        /// <returns>解析后的视图信息。</returns>
        ViewInfo GetView(XmlNode node);
    }
}

namespace Bing.Pdm.Abstractions
{
    using Bing.Pdm.Abstractions.Loaders;

    /// <summary>
    /// 提供旧版 PDM 加载器所需的加载器集合。
    /// </summary>
    public interface ILoaderContext
    {
        /// <summary>
        /// 获取键加载器。
        /// </summary>
        IKeyLoader KeyLoader { get; }
        /// <summary>
        /// 获取模式加载器。
        /// </summary>
        ISchemaLoader SchemaLoader { get; }
        /// <summary>
        /// 获取包加载器。
        /// </summary>
        IPackageLoader PackageLoader { get; }
        /// <summary>
        /// 获取表加载器。
        /// </summary>
        ITableLoader TableLoader { get; }
        /// <summary>
        /// 获取视图加载器。
        /// </summary>
        IViewLoader ViewLoader { get; }
        /// <summary>
        /// 获取 PDM 加载器。
        /// </summary>
        IPdmLoader PdmLoader { get; }
        /// <summary>
        /// 获取数据库管理系统加载器。
        /// </summary>
        IDbmsLoader DbmsLoader { get; }
        /// <summary>
        /// 获取分组加载器。
        /// </summary>
        IGroupLoader GroupLoader { get; }
        /// <summary>
        /// 获取物理图加载器。
        /// </summary>
        IPhysicalDiagramLoader PhysicalDiagramLoader { get; }
        /// <summary>
        /// 获取引用加载器。
        /// </summary>
        IReferenceLoader ReferenceLoader { get; }
        /// <summary>
        /// 获取目标模型加载器。
        /// </summary>
        ITargetModelLoader TargetModelLoader { get; }
    }
}
