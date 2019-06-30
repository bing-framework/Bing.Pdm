using System;
using System.Xml;
using Bing.Pdm.Models;

namespace Bing.Pdm.Reader
{
    public class PdmReader : IPdmReader
    {
        public void ReadFromFile(string pdmFile)
        {
            // 加载文件
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(pdmFile);
            // 必须增加xml命名空间管理，否则读取会报错。
            var xmlnsManager = new XmlNamespaceManager(xmlDoc.NameTable);
            xmlnsManager.AddNamespace("a", "attribute");
            xmlnsManager.AddNamespace("c", "collection");
            xmlnsManager.AddNamespace("o", "object");

            // 读取所有表节点
            var tableList = xmlDoc.SelectNodes("//c:Tables", xmlnsManager);
            foreach (XmlNode tables in tableList)
            {
                foreach (XmlNode table in tables.ChildNodes)
                {
                    // 排除快捷对象
                    if (table.Name != "o:Shortcut")
                    {
                        // 加入表
                    }
                }
            }

            // 读取所有视图节点
            var viewList = xmlDoc.SelectNodes("//c:Views", xmlnsManager);
            foreach (XmlNode views in viewList)
            {
                foreach (XmlNode view in views.ChildNodes)
                {
                    // 加入视图
                }
            }
        }

        #region GetTable(获取表信息)

        /// <summary>
        /// 获取表信息
        /// </summary>
        /// <param name="node">节点</param>
        private TableInfo GetTable(XmlNode node)
        {
            var table = new TableInfo();
            var xe = (XmlElement)node;
            table.TableId = xe.GetAttribute("Id");
            var properties = xe.ChildNodes;
            foreach (XmlNode property in properties)
            {
                switch (property.Name)
                {
                    case "a:ObjectID":
                        table.ObjectId = property.InnerText;
                        break;
                    case "a:Name":
                        table.Name = property.InnerText;
                        break;
                    case "a:Code":
                        table.Code = property.InnerText;
                        break;
                    case "a:CreationDate":
                        table.CreationDate = String2DateTime(property.InnerText);
                        break;
                    case "a:Creator":
                        table.Creator = property.InnerText;
                        break;
                    case "a:ModificationDate":
                        table.ModificationDate = String2DateTime(property.InnerText);
                        break;
                    case "a:Modifier":
                        table.Modifier = property.InnerText;
                        break;
                    case "a:Comment":
                        table.Comment = property.InnerText;
                        break;
                    case "a:PhysicalOptions":
                        table.PhysicalOptions = property.InnerText;
                        break;
                    case "a:Description":
                        table.Description = property.InnerText;
                        break;
                    case "c:Columns":
                        InitColumns(property,table);
                        break;
                    case "c:Keys":
                        InitKeys(property,table);
                        break;
                    case "c:PrimaryKey":
                        InitPrimaryKey(property, table);
                        break;
                }
            }

            return table;
        }

        #endregion

        #region GetView(获取视图信息)

        /// <summary>
        /// 获取视图信息
        /// </summary>
        /// <param name="node">节点</param>
        private ViewInfo GetView(XmlNode node)
        {
            var view = new ViewInfo();
            var xe = (XmlElement)node;
            view.ViewId = xe.GetAttribute("Id");
            var properties = xe.ChildNodes;
            foreach (XmlNode property in properties)
            {
                switch (property.Name)
                {
                    case "a:ObjectID":
                        view.ObjectId = property.InnerText;
                        break;
                    case "a:Name":
                        view.Name = property.InnerText;
                        break;
                    case "a:Code":
                        view.Code = property.InnerText;
                        break;
                    case "a:CreationDate":
                        view.CreationDate = String2DateTime(property.InnerText);
                        break;
                    case "a:Creator":
                        view.Creator = property.InnerText;
                        break;
                    case "a:ModificationDate":
                        view.ModificationDate = String2DateTime(property.InnerText);
                        break;
                    case "a:Modifier":
                        view.Modifier = property.InnerText;
                        break;
                    case "a:Comment":
                        view.Comment = property.InnerText;
                        break;
                    case "a:Description":
                        view.Description = property.InnerText;
                        break;
                    case "a:View.SQLQuery":
                        view.ViewSQLQuery = property.InnerText;
                        break;
                    case "a:TaggedSQLQuery":
                        view.TaggedSQLQuery = property.InnerText;
                        break;
                    case "c:Columns":
                        InitColumns(property, view);
                        break;
                }
            }
            return view;
        }

        #endregion

        #region InitColumns(初始化列信息)

        /// <summary>
        /// 初始化表列信息
        /// </summary>
        /// <param name="columns">列节点集合</param>
        /// <param name="table">表信息</param>
        private void InitColumns(XmlNode columns, TableInfo table)
        {
            foreach (XmlNode column in columns)
                table.AddColumn(GetColumn(column, table));
        }

        /// <summary>
        /// 初始化视图列信息
        /// </summary>
        /// <param name="columns">列节点集合</param>
        /// <param name="view">视图信息</param>
        private void InitColumns(XmlNode columns, ViewInfo view)
        {
            foreach (XmlNode column in columns)
                view.Columns.Add(GetColumn(column,view));
        }

        #endregion

        #region InitKeys(初始化键信息)

        /// <summary>
        /// 初始化键信息
        /// </summary>
        /// <param name="keys">键节点集合</param>
        /// <param name="table">表信息</param>
        private void InitKeys(XmlNode keys, TableInfo table)
        {
            foreach (XmlNode key in keys)
                table.AddKey(GetKey(key, table));
        }

        #endregion

        #region GetKey(获取键信息)

        /// <summary>
        /// 获取键信息
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="ownerTable">所有者表信息</param>
        private PdmKey GetKey(XmlNode node, TableInfo ownerTable)
        {
            var key = new PdmKey(ownerTable);
            var xe = (XmlElement)node;
            key.KeyId = xe.GetAttribute("Id");
            var properties = xe.ChildNodes;
            foreach (XmlNode property in properties)
            {
                switch (property.Name)
                {
                    case "a:ObjectID":
                        key.ObjectId = property.InnerText;
                        break;
                    case "a:Name":
                        key.Name = property.InnerText;
                        break;
                    case "a:Code":
                        key.Code = property.InnerText;
                        break;
                    case "a:CreationDate":
                        key.CreationDate = String2DateTime(property.InnerText);
                        break;
                    case "a:Creator":
                        key.Creator = property.InnerText;
                        break;
                    case "a:ModificationDate":
                        key.ModificationDate = String2DateTime(property.InnerText);
                        break;
                    case "a:Modifier":
                        key.Modifier = property.InnerText;
                        break;
                    case "c:Key.Columns":
                        InitKeyColumns(property,key);
                        break;
                }
            }

            return key;
        }

        #endregion

        #region InitKeyColumns(初始化键列集合)

        /// <summary>
        /// 初始化键列结婚
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="key">键</param>
        private void InitKeyColumns(XmlNode node, PdmKey key)
        {
            var xe = (XmlElement)node;
            foreach (XmlNode property in xe.ChildNodes)
            {
                key.ColumnObjCodes.Add(((XmlElement) property).GetAttribute("Ref"));
            }
        }

        #endregion

        #region InitPrimaryKey(初始化主键)

        /// <summary>
        /// 初始化主键
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="table">所有者表信息</param>
        private void InitPrimaryKey(XmlNode node, TableInfo table)
        {
            table.PrimaryKeyRefCode = GetPrimaryKey(node);
        }

        #endregion

        #region GetPrimaryKey(获取主键)

        /// <summary>
        /// 获取主键
        /// </summary>
        /// <param name="node">节点</param>
        private string GetPrimaryKey(XmlNode node)
        {
            var xe = (XmlElement)node;
            if (xe.ChildNodes.Count > 0)
            {
                var pk = (XmlElement)xe.ChildNodes[0];
                return pk.GetAttribute("Ref");
            }

            return string.Empty;
        }

        #endregion

        #region GetColumn(获取列信息)

        /// <summary>
        /// 获取表列信息
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="ownerTable">所有者表信息</param>
        private ColumnInfo GetColumn(XmlNode node, TableInfo ownerTable)
        {
            var column = new ColumnInfo(ownerTable);
            var xe = (XmlElement)node;
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
                    case "a:Length":
                        column.Length = property.InnerText;
                        break;
                    case "a.Identity":
                        column.Identity = ConvertToBoolean(property.InnerText);
                        break;
                    case "a:Column.Mandatory":
                        column.Mandatory = ConvertToBoolean(property.InnerText);
                        break;
                    case "a:PhysicalOptions":
                        column.PhysicalOptions = property.InnerText;
                        break;
                    case "a:ExtendedAttributesText":
                        column.ExtendedAttributesText = property.InnerText;
                        break;
                    case "a:Precision":
                        column.Precision = property.InnerText;
                        break;
                    case "a:Description":
                        column.Description = property.InnerText;
                        break;
                }
            }

            return column;
        }

        /// <summary>
        /// 获取视图列信息
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="ownerView">所有者视图信息</param>
        private ViewColumnInfo GetColumn(XmlNode node, ViewInfo ownerView)
        {
            var column = new ViewColumnInfo(ownerView);
            var xe = (XmlElement)node;
            column.ViewColumnId = xe.GetAttribute("Id");
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
                    case "a:Length":
                        column.Length = property.InnerText;
                        break;
                    case "a:Description":
                        column.Description = property.InnerText;
                        break;
                }
            }

            return column;
        }

        #endregion

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
