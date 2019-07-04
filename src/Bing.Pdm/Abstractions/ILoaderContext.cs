using Bing.Pdm.Abstractions.Loaders;

namespace Bing.Pdm.Abstractions
{
    /// <summary>
    /// 加载器上下文
    /// </summary>
    public interface ILoaderContext
    {
        /// <summary>
        /// Key加载器
        /// </summary>
        IKeyLoader KeyLoader { get; }

        /// <summary>
        /// 架构加载器
        /// </summary>
        ISchemaLoader SchemaLoader { get; }

        /// <summary>
        /// Package加载器
        /// </summary>
        IPackageLoader PackageLoader { get; }

        /// <summary>
        /// 数据表加载器
        /// </summary>
        ITableLoader TableLoader { get; }

        /// <summary>
        /// 视图加载器
        /// </summary>
        IViewLoader ViewLoader { get; }

        /// <summary>
        /// PDM加载器
        /// </summary>
        IPdmLoader PdmLoader { get; }

        /// <summary>
        /// DBMS加载器
        /// </summary>
        IDbmsLoader DbmsLoader { get; }

        /// <summary>
        /// 组加载器
        /// </summary>
        IGroupLoader GroupLoader { get; }

        /// <summary>
        /// 物理图加载器
        /// </summary>
        IPhysicalDiagramLoader PhysicalDiagramLoader { get; }

        /// <summary>
        /// 引用加载器
        /// </summary>
        IReferenceLoader ReferenceLoader { get; }

        /// <summary>
        /// 目标模型加载器
        /// </summary>
        ITargetModelLoader TargetModelLoader { get; }
    }
}
