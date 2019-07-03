using System.Xml;
using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Extensions;
using Bing.Pdm.Models;

namespace Bing.Pdm.Core.Loaders
{
    /// <summary>
    /// DBMS加载器
    /// </summary>
    internal class DbmsLoader : IDbmsLoader
    {
        /// <summary>
        /// 获取DBMS
        /// </summary>
        /// <param name="node">节点</param>
        public DbmsInfo GetDbms(XmlNode node)
        {
            var dbms = new DbmsInfo();
            var xe = (XmlElement) node;
            dbms.DbmsId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                property.CommonInfoHandle(dbms);
                switch (property.Name)
                {
                    case Const.ATargetId:
                        dbms.TargetId = property.InnerText;
                        break;
                    case Const.ATargetClassId:
                        dbms.TargetClassId = property.InnerText;
                        break;
                }
            }

            return dbms;
        }
    }
}
