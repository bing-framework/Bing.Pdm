using System.Xml;
using Bing.Pdm.Models;

namespace Bing.Pdm.Abstractions.Loaders
{
    /// <summary>
    /// 组加载器
    /// </summary>
    public interface IGroupLoader
    {
        /// <summary>
        /// 获取组信息
        /// </summary>
        /// <param name="node">节点</param>
        GroupInfo GetGroup(XmlNode node);
    }
}
