using System.Xml;
using Bing.Pdm.Models;

namespace Bing.Pdm.Abstractions.Loaders
{
    /// <summary>
    /// 架构加载器
    /// </summary>
    public interface ISchemaLoader
    {
        /// <summary>
        /// 获取架构
        /// </summary>
        /// <param name="node">节点</param>
        SchemaInfo GetSchema(XmlNode node);
    }
}
