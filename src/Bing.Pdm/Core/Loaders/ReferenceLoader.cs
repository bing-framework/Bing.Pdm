using System.Xml;
using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Extensions;
using Bing.Pdm.Models.References;

namespace Bing.Pdm.Core.Loaders
{
    /// <summary>
    /// 引用加载器
    /// </summary>
    internal class ReferenceLoader : IReferenceLoader
    {
        /// <summary>
        /// 获取引用
        /// </summary>
        /// <param name="node">节点</param>
        public ReferenceInfo GetReference(XmlNode node)
        {
            var reference = new ReferenceInfo();
            var xe = (XmlElement) node;
            reference.ReferenceId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                property.CommonInfoHandle(reference);
                switch (property.Name)
                {
                    case Const.ACardinality:
                        reference.Cardinality = property.InnerText;
                        break;
                    case Const.CParentTable:
                        break;
                    case Const.CChildTable:
                        break;
                    case Const.CParentKey:
                        break;
                    case Const.CJoins:
                        InitJoins(property,reference);
                        break;
                }
            }

            return reference;
        }

        /// <summary>
        /// 初始化引用关联信息
        /// </summary>
        /// <param name="joins">引用关联节点集合</param>
        /// <param name="reference">引用信息</param>
        private void InitJoins(XmlNode joins, ReferenceInfo reference)
        {
            foreach (XmlNode join in joins)
            {
                reference.Joins.Add(GetReferenceJoin(join));
            }
        }

        /// <summary>
        /// 获取引用关联
        /// </summary>
        /// <param name="node">节点</param>
        private ReferenceJoinInfo GetReferenceJoin(XmlNode node)
        {
            var referenceJoin = new ReferenceJoinInfo();
            var xe = (XmlElement) node;
            referenceJoin.ReferenceJoinId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                property.CommonInfoHandle(referenceJoin);
                switch (property.Name)
                {
                    case Const.CObject1:
                        break;
                    case Const.CObject2:
                        break;
                }
            }

            return referenceJoin;
        }
    }
}
