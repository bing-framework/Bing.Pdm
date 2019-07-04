using System.Xml;
using Bing.Pdm.Models.Others;

namespace Bing.Pdm.Abstractions.Loaders
{
    /// <summary>
    /// 目标模型加载器
    /// </summary>
    public interface ITargetModelLoader
    {
        /// <summary>
        /// 获取目标模型
        /// </summary>
        /// <param name="node">节点</param>
        TargetModelInfo GetTargetModel(XmlNode node);
    }
}
