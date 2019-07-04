using System.Xml;
using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Extensions;
using Bing.Pdm.Models.Others;
using Bing.Pdm.Models.PhysicalDiagrams;

namespace Bing.Pdm.Core.Loaders
{
    /// <summary>
    /// 物理图加载器
    /// </summary>
    internal class PhysicalDiagramLoader : IPhysicalDiagramLoader
    {
        /// <summary>
        /// 获取物理图
        /// </summary>
        /// <param name="node">节点</param>
        public PhysicalDiagramInfo GetPhysicalDiagram(XmlNode node)
        {
            var physicalDiagram = new PhysicalDiagramInfo();
            var xe = (XmlElement) node;
            physicalDiagram.PhysicalDiagramId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                property.CommonInfoHandle(physicalDiagram);
                switch (property.Name)
                {
                    case Const.ADisplayPreferences:
                        //physicalDiagram.DisplayPreferences = property.InnerText;
                        break;
                    case Const.APaperSize:
                        physicalDiagram.PagerSize = property.InnerText;
                        break;
                    case Const.APageMargins:
                        physicalDiagram.PageMargins = property.InnerText;
                        break;
                    case Const.APageOrientation:
                        physicalDiagram.PageOrientation = property.InnerText.ToInt();
                        break;
                    case Const.APaperSource:
                        physicalDiagram.PaperSource = property.InnerText.ToInt();
                        break;
                }
            }

            return physicalDiagram;
        }
    }
}
