using System.Xml;
using Bing.Pdm.Abstractions;
using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Extensions;
using Bing.Pdm.Models;

namespace Bing.Pdm.Core.Loaders
{
    /// <summary>
    /// PDM加载器
    /// </summary>
    internal class PdmLoader : IPdmLoader
    {
        /// <summary>
        /// 加载器上下文
        /// </summary>
        private readonly ILoaderContext _context;
        
        /// <summary>
        /// 初始化一个<see cref="PdmLoader"/>类型的实例
        /// </summary>
        /// <param name="context">加载器上下文</param>
        public PdmLoader(ILoaderContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 获取PDM信息
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public PdmInfo GetPdm(string filePath)
        {
            // 加载文件
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(filePath);
            // 必须增加xml命名空间管理，否则读取会报错。
            var xmlnsManager = new XmlNamespaceManager(xmlDoc.NameTable);
            xmlnsManager.AddNamespace("a", Const.A);
            xmlnsManager.AddNamespace("c", Const.C);
            xmlnsManager.AddNamespace("o", Const.O);

            var pdmInfo = new PdmInfo();
            var pdmNode = xmlDoc.SelectSingleNode($"//{Const.CChildren}/{Const.OModel}", xmlnsManager);
            if (pdmNode == null)
                return pdmInfo;
            return GetPdm(pdmNode);
            //// 读取所有表节点
            //var tableList = xmlDoc.SelectNodes($"//{Const.CTables}", xmlnsManager);
            //if (tableList != null)
            //{
            //    foreach (XmlNode tables in tableList)
            //    {
            //        foreach (XmlNode table in tables.ChildNodes)
            //        {
            //            // 排除快捷对象
            //            if (table.Name != "o:Shortcut")
            //            {
            //                pdmInfo.Tables.Add(_context.TableLoader.GetTable(table));
            //            }
            //        }
            //    }
            //}

            //// 读取所有视图节点
            //var viewList = xmlDoc.SelectNodes($"//{Const.CViews}", xmlnsManager);
            //if (viewList != null)
            //{
            //    foreach (XmlNode views in viewList)
            //    {
            //        foreach (XmlNode view in views.ChildNodes)
            //        {
            //            pdmInfo.Views.Add(_context.ViewLoader.GetView(view));
            //        }
            //    }
            //}
        }

        /// <summary>
        /// 获取PDM信息
        /// </summary>
        /// <param name="node">节点</param>
        private PdmInfo GetPdm(XmlNode node)
        {
            var pdm = new PdmInfo();
            var xe = (XmlElement)node;
            pdm.PdmId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                property.CommonInfoHandle(pdm);
                switch (property.Name)
                {
                    case Const.APackageOptionsText:
                        //pdm.PackageOptionsText = property.InnerText;
                        break;
                    case Const.AModelOptionsText:
                        //pdm.ModelOptionsText = property.InnerText;
                        break;
                    case Const.AAuthor:
                        pdm.Author = property.InnerText;
                        break;
                    case Const.AVersion:
                        pdm.Version = property.InnerText;
                        break;
                    case Const.ARepositoryFilename:
                        pdm.RepositoryFileName = property.InnerText;
                        break;
                    case Const.CDBMS:
                        InitDbms(property,pdm);
                        break;
                    case Const.CPhysicalDiagrams:
                        InitPhysicalDiagrams(property, pdm);
                        break;
                    case Const.CPackages:
                        InitPackages(property, pdm);
                        break;
                    case Const.CDefaultDiagarm:
                        break;
                    case Const.CUsers:
                        InitSchemas(property, pdm);
                        break;
                    case Const.CDefaultGroups:
                        InitGroups(property, pdm);
                        break;
                    case Const.CTargetModels:
                        InitTargetModels(property, pdm);
                        break;
                }
            }

            return pdm;
        }

        /// <summary>
        /// 初始化DBMS
        /// </summary>
        /// <param name="dbms">DBMS节点集合</param>
        /// <param name="pdm">PDM信息</param>
        private void InitDbms(XmlNode dbms, PdmInfo pdm)
        {
            foreach (XmlNode node in dbms)
            {
                var result = _context.DbmsLoader.GetDbms(node);
                if (result != null)
                {
                    pdm.Dbms = result;
                    return;
                }
            }
        }

        /// <summary>
        /// 初始化物理图信息
        /// </summary>
        /// <param name="physicalDiagrams">物理图节点集合</param>
        /// <param name="pdm">PDM信息</param>
        private void InitPhysicalDiagrams(XmlNode physicalDiagrams, PdmInfo pdm)
        {
            foreach (XmlNode physicalDiagram in physicalDiagrams)
            {
                pdm.PhysicalDiagrams.Add(_context.PhysicalDiagramLoader.GetPhysicalDiagram(physicalDiagram));
            }
        }

        /// <summary>
        /// 初始化Package信息
        /// </summary>
        /// <param name="packages">Package节点集合</param>
        /// <param name="pdm">PDM信息</param>
        private void InitPackages(XmlNode packages, PdmInfo pdm)
        {
            foreach (XmlNode package in packages)
            {
                pdm.PackageInfos.Add(_context.PackageLoader.GetPackage(package));
            }
        }

        /// <summary>
        /// 初始化架构信息
        /// </summary>
        /// <param name="schemas">架构节点集合</param>
        /// <param name="pdm">PDM信息</param>
        private void InitSchemas(XmlNode schemas, PdmInfo pdm)
        {
            foreach (XmlNode schema in schemas)
            {
                pdm.Schemas.Add(_context.SchemaLoader.GetSchema(schema));
            }
        }

        /// <summary>
        /// 初始化组信息
        /// </summary>
        /// <param name="groups">组节点集合</param>
        /// <param name="pdm">PDM信息</param>
        private void InitGroups(XmlNode groups, PdmInfo pdm)
        {
            foreach (XmlNode group in groups)
            {
                pdm.DefaultGroups.Add(_context.GroupLoader.GetGroup(group));
            }
        }

        /// <summary>
        /// 初始化目标模型信息
        /// </summary>
        /// <param name="targetModels">目标模型节点集合</param>
        /// <param name="pdm">PDM信息</param>
        private void InitTargetModels(XmlNode targetModels, PdmInfo pdm)
        {
            foreach (XmlNode targetModel in targetModels)
            {
                pdm.TargetModels.Add(_context.TargetModelLoader.GetTargetModel(targetModel));
            }
        }
    }
}
