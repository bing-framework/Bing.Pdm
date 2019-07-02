using System;
using System.Xml;
using Bing.Pdm.Models;

namespace Bing.Pdm.Extensions
{
    /// <summary>
    /// 内部扩展
    /// </summary>
    internal static class InternalExtensions
    {
        /// <summary>
        /// 1970年1月1日。PDM文件中的日期格式采用的是当前日期与1970年1月1日8点之差的秒数来保存.
        /// </summary>
        internal static readonly DateTime Date1970 = new DateTime(1970, 1, 1, 0, 0, 0);

        /// <summary>
        /// 转换为时间
        /// </summary>
        /// <param name="timestamp">时间戳</param>
        public static DateTime ToDateTime(this string timestamp)
        {
            var ticker = long.Parse(timestamp);
            return Date1970.AddSeconds(ticker);
        }

        /// <summary>
        /// 转换为布尔值
        /// </summary>
        /// <param name="obj">对象</param>
        public static bool ToBoolean(this object obj)
        {
            if (obj != null)
            {
                var str = obj.ToString();
                str = str.ToLower();
                if (str.Equals("y") || str.Equals("1") || str.Equals("true"))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 通用信息处理
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="node">节点</param>
        /// <param name="entity">PDM对象</param>
        public static void CommonInfoHandle<T>(this XmlNode node, T entity) where T : class
        {
            if (entity is PdmCommonInfo pdmCommonInfo)
            {
                switch (node.Name)
                {
                    case Const.AObjectId:
                        pdmCommonInfo.ObjectId = node.InnerText;
                        break;
                    case Const.AName:
                        pdmCommonInfo.Name = node.InnerText;
                        break;
                    case Const.ACode:
                        pdmCommonInfo.Code = node.InnerText;
                        break;
                    case Const.ACreationDate:
                        pdmCommonInfo.CreationDate = node.InnerText.ToDateTime();
                        break;
                    case Const.ACreator:
                        pdmCommonInfo.Creator = node.InnerText;
                        break;
                    case Const.AModificationDate:
                        pdmCommonInfo.ModificationDate = node.InnerText.ToDateTime();
                        break;
                    case Const.AModifier:
                        pdmCommonInfo.Modifier = node.InnerText;
                        break;
                }
            }

            switch (node.Name)
            {
                case Const.AComment when entity is IComment comment:
                    comment.Comment = node.InnerText;
                    return;
                case Const.ADescription when entity is IDescription description:
                    description.Description = node.InnerText;
                    return;
            }
        }
    }
}
