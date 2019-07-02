using System.Xml;
using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Extensions;
using Bing.Pdm.Models;

namespace Bing.Pdm.Core.Loaders
{
    /// <summary>
    /// 架构加载器
    /// </summary>
    internal class SchemaLoader : ISchemaLoader
    {
        /// <summary>
        /// 获取架构
        /// </summary>
        /// <param name="node">节点</param>
        public SchemaInfo GetSchema(XmlNode node)
        {
            var schema = new SchemaInfo();
            var xe = (XmlElement)node;
            schema.SchemaId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                property.CommonInfoHandle(schema);
                switch (property.Name)
                {
                    case Const.AStereotype:
                        schema.StereoType = property.InnerText;
                        break;
                }
            }

            return schema;
        }
    }
}
