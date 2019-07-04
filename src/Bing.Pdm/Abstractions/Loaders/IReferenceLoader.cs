using System.Xml;
using Bing.Pdm.Models.References;

namespace Bing.Pdm.Abstractions.Loaders
{
    /// <summary>
    /// 引用加载器
    /// </summary>
    public interface IReferenceLoader
    {
        /// <summary>
        /// 获取引用
        /// </summary>
        /// <param name="node">节点</param>
        ReferenceInfo GetReference(XmlNode node);
    }
}
