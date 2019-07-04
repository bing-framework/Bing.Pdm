using System.Xml;
using Bing.Pdm.Models.Others;
using Bing.Pdm.Models.PhysicalDiagrams;

namespace Bing.Pdm.Abstractions.Loaders
{
    /// <summary>
    /// 物理图加载器
    /// </summary>
    public interface IPhysicalDiagramLoader
    {
        /// <summary>
        /// 获取物理图
        /// </summary>
        /// <param name="node">节点</param>
        PhysicalDiagramInfo GetPhysicalDiagram(XmlNode node);
    }
}
