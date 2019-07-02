using System.Xml;
using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Extensions;
using Bing.Pdm.Models;

namespace Bing.Pdm.Core.Loaders
{
    /// <summary>
    /// Package加载器
    /// </summary>
    internal class PackageLoader : IPackageLoader
    {
        /// <summary>
        /// 获取Package
        /// </summary>
        /// <param name="node">节点</param>
        public PackageInfo GetPackage(XmlNode node)
        {
            var package = new PackageInfo();
            var xe = (XmlElement)node;
            package.PackageId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                property.CommonInfoHandle(package);
            }

            return package;
        }
    }
}
