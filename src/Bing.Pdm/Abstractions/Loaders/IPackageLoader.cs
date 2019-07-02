using System.Xml;
using Bing.Pdm.Models;

namespace Bing.Pdm.Abstractions.Loaders
{
    /// <summary>
    /// Package加载器
    /// </summary>
    public interface IPackageLoader
    {
        /// <summary>
        /// 获取Package
        /// </summary>
        /// <param name="node">节点</param>
        PackageInfo GetPackage(XmlNode node);
    }
}
