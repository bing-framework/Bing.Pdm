using System.Xml;
using Bing.Pdm.Models.Views;

namespace Bing.Pdm.Abstractions.Loaders
{
    /// <summary>
    /// 视图加载器
    /// </summary>
    public interface IViewLoader
    {
        /// <summary>
        /// 获取视图
        /// </summary>
        /// <param name="node">节点</param>
        ViewInfo GetView(XmlNode node);
    }
}
