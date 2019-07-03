using System.Xml;
using Bing.Pdm.Models;

namespace Bing.Pdm.Abstractions.Loaders
{
    /// <summary>
    /// DBMS加载器
    /// </summary>
    public interface IDbmsLoader
    {
        /// <summary>
        /// 获取DBMS
        /// </summary>
        /// <param name="node">节点</param>
        DbmsInfo GetDbms(XmlNode node);
    }
}
