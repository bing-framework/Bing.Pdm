using System.Xml;
using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Extensions;
using Bing.Pdm.Models;
using Bing.Pdm.Models.Others;

namespace Bing.Pdm.Core.Loaders
{
    /// <summary>
    /// 组加载器
    /// </summary>
    internal class GroupLoader : IGroupLoader
    {
        /// <summary>
        /// 获取组信息
        /// </summary>
        /// <param name="node">节点</param>
        public GroupInfo GetGroup(XmlNode node)
        {
            var group = new GroupInfo();
            var xe = (XmlElement)node;
            group.GroupId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                property.CommonInfoHandle(group);
            }

            return group;
        }
    }
}
