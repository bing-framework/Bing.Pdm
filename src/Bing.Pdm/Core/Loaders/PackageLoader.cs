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
        /// 数据表加载器
        /// </summary>
        private readonly ITableLoader _tableLoader;

        /// <summary>
        /// 引用加载器
        /// </summary>
        private readonly IReferenceLoader _referenceLoader;

        /// <summary>
        /// 初始化一个<see cref="PackageLoader"/>类型的实例
        /// </summary>
        /// <param name="tableLoader">数据表加载器</param>
        /// <param name="referenceLoader">引用加载器</param>
        public PackageLoader(ITableLoader tableLoader, IReferenceLoader referenceLoader)
        {
            _tableLoader = tableLoader;
            _referenceLoader = referenceLoader;
        }

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
                switch (property.Name)
                {
                    case Const.APackageOptionsText:
                        //package.PackageOptionsText = property.InnerText;
                        break;
                    case Const.CPhysicalDiagrams:
                        break;
                    case Const.CDefaultDiagarm:
                        break;
                    case Const.CTables:
                        InitTables(property, package);
                        break;
                    case Const.CReferences:
                        InitReferences(property, package);
                        break;
                }
            }

            return package;
        }

        /// <summary>
        /// 初始化表信息
        /// </summary>
        /// <param name="tables">数据表节点集合</param>
        /// <param name="package">Package信息</param>
        private void InitTables(XmlNode tables, PackageInfo package)
        {
            foreach (XmlNode table in tables)
            {
                package.Tables.Add(_tableLoader.GetTable(table));
            }
        }

        /// <summary>
        /// 初始化引用信息
        /// </summary>
        /// <param name="references">引用节点集合</param>
        /// <param name="package">Package信息</param>
        private void InitReferences(XmlNode references, PackageInfo package)
        {
            foreach (XmlNode reference in references)
            {
                package.References.Add(_referenceLoader.GetReference(reference));
            }
        }
    }
}
