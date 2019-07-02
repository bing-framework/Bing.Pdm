using System.Xml;
using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Models.Keys;
using Bing.Pdm.Models.Tables;

namespace Bing.Pdm.Core.Loaders
{
    /// <summary>
    /// Key加载器
    /// </summary>
    internal class KeyLoader : IKeyLoader
    {
        /// <summary>
        /// 获取Key
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="ownerTable">所有者数据表</param>
        public KeyInfo GetKey(XmlNode node, TableInfo ownerTable)
        {
            var key = new KeyInfo(ownerTable);
            var xe = (XmlElement)node;
            key.KeyId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                switch (property.Name)
                {
                    case Const.CKeyColumns:
                        InitKeyColumns(property, key);
                        break;
                }
            }

            return key;
        }

        /// <summary>
        /// 初始化Key列集合
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="key">Key</param>
        private void InitKeyColumns(XmlNode node, KeyInfo key)
        {
            var xe = (XmlElement)node;
            foreach (XmlNode property in xe.ChildNodes)
            {
                key.ColumnRefIds.Add(((XmlElement)property).GetAttribute(Const.Ref));
            }
        }
    }
}
