using Bing.Pdm.Abstractions;
using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Core.Loaders;

namespace Bing.Pdm.Core
{
    /// <summary>
    /// 加载器上下文
    /// </summary>
    public class LoaderContext : ILoaderContext
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
        /// 初始化一个<see cref="LoaderContext"/>类型的实例
        /// </summary>
        public LoaderContext()
        {
            KeyLoader = new KeyLoader();
            SchemaLoader = new SchemaLoader();
            PackageLoader = new PackageLoader();
            TableLoader = new TableLoader(KeyLoader);
            ViewLoader = new ViewLoader();
            PdmLoader = new PdmLoader(this);
        }
    }
}
