using System.Xml;
using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Extensions;
using Bing.Pdm.Models.Others;

namespace Bing.Pdm.Core.Loaders
{
    /// <summary>
    /// 目标模型加载器
    /// </summary>
    internal class TargetModelLoader : ITargetModelLoader
    {
        /// <summary>
        /// 获取目标模型
        /// </summary>
        /// <param name="node">节点</param>
        public TargetModelInfo GetTargetModel(XmlNode node)
        {
            var targetModel = new TargetModelInfo();
            var xe = (XmlElement)node;
            targetModel.TargetModelId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                property.CommonInfoHandle(targetModel);
                switch (property.Name)
                {
                    case Const.ATargetModelUrl:
                        targetModel.TargetUrl = property.InnerText;
                        break;
                    case Const.ATargetModelId:
                        targetModel.TargetId = property.InnerText;
                        break;
                    case Const.ATargetModelClassId:
                        targetModel.TargetClassId = property.InnerText;
                        break;
                    case Const.ATargetModelLastModificationDate:
                        targetModel.TargetLastModificationDate = property.InnerText.ToDateTime();
                        break;
                }
            }

            return targetModel;
        }
    }
}
