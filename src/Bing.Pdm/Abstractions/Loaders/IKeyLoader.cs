using System.Xml;
using Bing.Pdm.Models.Keys;
using Bing.Pdm.Models.Tables;

namespace Bing.Pdm.Abstractions.Loaders
{
    /// <summary>
    /// Key加载器
    /// </summary>
    public interface IKeyLoader
    {
        /// <summary>
        /// 获取Key
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="ownerTable">所有者数据表</param>
        KeyInfo GetKey(XmlNode node, TableInfo ownerTable);
    }
}
