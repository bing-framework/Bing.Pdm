using System.Xml;
using Bing.Pdm.Models.Tables;

namespace Bing.Pdm.Abstractions.Loaders
{
    /// <summary>
    /// 数据表加载器
    /// </summary>
    public interface ITableLoader
    {
        /// <summary>
        /// 获取数据表
        /// </summary>
        /// <param name="node">节点</param>
        TableInfo GetTable(XmlNode node);
    }
}
