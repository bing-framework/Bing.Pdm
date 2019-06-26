using System;
using System.Xml;
using Bing.Pdm.Models;

namespace Bing.Pdm.Reader
{
    public class PdmReader : IPdmReader
    {
        public void ReadFromFile(string pdmFile)
        {
            throw new System.NotImplementedException();
        }

        private ColumnInfo GetColumn(XmlNode node, TableInfo ownerTable)
        {
            var column = new ColumnInfo(ownerTable);
            var xe = (XmlElement) node;
            column.ColumnId = xe.GetAttribute("Id");
            var properties = xe.ChildNodes;
            foreach (XmlNode property in properties)
            {
                switch (property.Name)
                {
                    case "a:ObjectID":
                        column.ObjectId = property.InnerText;
                        break;
                    case "a:Name":
                        column.Name = property.InnerText;
                        break;
                    case "a:Code":
                        column.Code = property.InnerText;
                        break;
                    case "a:CreationDate":
                        column.CreationDate = String2DateTime(property.InnerText);
                        break;
                    case "a:Creator":
                        column.Creator = property.InnerText;
                        break;
                    case "a:ModificationDate":
                        column.ModificationDate = String2DateTime(property.InnerText);
                        break;
                    case "a:Modifier":
                        column.Modifier = property.InnerText;
                        break;
                    case "a:Comment":
                        column.Comment = property.InnerText;
                        break;
                    case "a:DataType":
                        column.DataType = property.InnerText;
                        break;
                    case "a:Column.Mandatory":
                        column.Mandatory = ConvertToBoolean(property.InnerText);
                        break;
                }
            }

            return column;
        }

        /// <summary>
        /// 1970年1月1日。PDM文件中的日期格式采用的是当前日期与1970年1月1日8点之差的秒数来保存.
        /// </summary>
        internal static readonly DateTime Date1970 = new DateTime(1970, 1, 1, 0, 0, 0);

        /// <summary>
        /// 字符串转时间
        /// </summary>
        /// <param name="dateString">时间戳字符串</param>
        private static DateTime String2DateTime(string dateString)
        {
            var ticker = long.Parse(dateString);
            return Date1970.AddSeconds(ticker);
        }

        /// <summary>
        /// 转换为布尔值
        /// </summary>
        /// <param name="obj">对象</param>
        private static bool ConvertToBoolean(object obj)
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
    }
}
