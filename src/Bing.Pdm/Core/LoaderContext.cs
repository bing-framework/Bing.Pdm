using Bing.Pdm.Abstractions;
using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Core.Loaders;

namespace Bing.Pdm.Core
{
    /// <summary>
    /// 加载器上下文
    /// </summary>
    internal class LoaderContext : ILoaderContext
    {
        /// <summary>
        /// Key加载器
        /// </summary>
        public IKeyLoader KeyLoader { get; }

        /// <summary>
        /// 架构加载器
        /// </summary>
        public ISchemaLoader SchemaLoader { get; }

        /// <summary>
        /// Package加载器
        /// </summary>
        public IPackageLoader PackageLoader { get; }

        /// <summary>
        /// 数据表加载器
        /// </summary>
        public ITableLoader TableLoader { get; }

        /// <summary>
        /// 视图加载器
        /// </summary>
        public IViewLoader ViewLoader { get; }

        /// <summary>
        /// PDM加载器
        /// </summary>
        public IPdmLoader PdmLoader { get; }

        /// <summary>
        /// DBMS加载器
        /// </summary>
        public IDbmsLoader DbmsLoader { get; }

        /// <summary>
        /// 组加载器
        /// </summary>
        public IGroupLoader GroupLoader { get; }

        /// <summary>
        /// 物理图加载器
        /// </summary>
        public IPhysicalDiagramLoader PhysicalDiagramLoader { get; }

        /// <summary>
        /// 引用加载器
        /// </summary>
        public IReferenceLoader ReferenceLoader { get; }

        /// <summary>
        /// 目标模型加载器
        /// </summary>
        public ITargetModelLoader TargetModelLoader { get; }

        /// <summary>
        /// 初始化一个<see cref="LoaderContext"/>类型的实例
        /// </summary>
        public LoaderContext()
        {
            KeyLoader = new KeyLoader();
            ReferenceLoader = new ReferenceLoader();
            DbmsLoader = new DbmsLoader();
            GroupLoader = new GroupLoader();
            PhysicalDiagramLoader = new PhysicalDiagramLoader();
            ViewLoader = new ViewLoader();
            SchemaLoader = new SchemaLoader();
            TargetModelLoader = new TargetModelLoader();

            TableLoader = new TableLoader(KeyLoader);
            PackageLoader = new PackageLoader(TableLoader, ReferenceLoader);
            
            PdmLoader = new PdmLoader(this);
        }
    }
}
