using System;
using System.Xml;
using Bing.Pdm.Models;

namespace Bing.Pdm.Reader
{
    public class PdmReader : IPdmReader
    {
        public PdmInfo ReadFromFile(string pdmFile)
        {
            // 加载文件
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(pdmFile);
            // 必须增加xml命名空间管理，否则读取会报错。
            var xmlnsManager = new XmlNamespaceManager(xmlDoc.NameTable);
            xmlnsManager.AddNamespace("a", Const.A);
            xmlnsManager.AddNamespace("c", Const.C);
            xmlnsManager.AddNamespace("o", Const.O);

            var pdmInfo = new PdmInfo();
            var pdmNode = xmlDoc.SelectSingleNode($"//{Const.CChildren}/{Const.OModel}", xmlnsManager);
            if (pdmNode != null)
            {
                pdmInfo = GetPdm(pdmNode);
            }

            // 读取所有架构节点
            var schemaList = xmlDoc.SelectNodes($"//{Const.CUsers}", xmlnsManager);
            if (schemaList != null)
            {
                foreach (XmlNode schemas in schemaList)
                {
                    foreach (XmlNode schema in schemas.ChildNodes)
                    {
                        pdmInfo.Schemas.Add(GetSchema(schema));
                    }
                }
            }

            // 读取所有表节点
            var tableList = xmlDoc.SelectNodes($"//{Const.CTables}", xmlnsManager);
            if (tableList != null)
            {
                foreach (XmlNode tables in tableList)
                {
                    foreach (XmlNode table in tables.ChildNodes)
                    {
                        // 排除快捷对象
                        if (table.Name != "o:Shortcut")
                        {
                            pdmInfo.Tables.Add(GetTable(table));
                        }
                    }
                }
            }

            // 读取所有视图节点
            var viewList = xmlDoc.SelectNodes($"//{Const.CViews}", xmlnsManager);
            if (viewList != null)
            {
                foreach (XmlNode views in viewList)
                {
                    foreach (XmlNode view in views.ChildNodes)
                    {
                        pdmInfo.Views.Add(GetView(view));
                    }
                }
            }

            return pdmInfo;
        }

        #region GetPdm(获取Pdm信息)

        /// <summary>
        /// 获取Pdm信息
        /// </summary>
        /// <param name="node">节点</param>
        private PdmInfo GetPdm(XmlNode node)
        {
            var pdm = new PdmInfo();
            var xe = (XmlElement)node;
            pdm.PdmId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                CommonInfoHandle(property, pdm);
                switch (property.Name)
                {
                    case Const.AAuthor:
                        pdm.Author = property.InnerText;
                        break;
                    case Const.AVersion:
                        pdm.Version = property.InnerText;
                        break;
                    case Const.CPackages:
                        InitPackages(property,pdm);
                        break;
                }
            }

            return pdm;
        }

        #endregion

        #region GetSchema(获取架构信息)

        /// <summary>
        /// 获取架构信息
        /// </summary>
        /// <param name="node">节点</param>
        private SchemaInfo GetSchema(XmlNode node)
        {
            var schema = new SchemaInfo();
            var xe = (XmlElement)node;
            schema.SchemaId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                CommonInfoHandle(property, schema);
                switch (property.Name)
                {
                    case Const.AStereotype:
                        schema.StereoType = property.InnerText;
                        break;
                }
            }

            return schema;
        }

        #endregion

        #region GetTable(获取表信息)

        /// <summary>
        /// 获取表信息
        /// </summary>
        /// <param name="node">节点</param>
        private TableInfo GetTable(XmlNode node)
        {
            var table = new TableInfo();
            var xe = (XmlElement)node;
            table.TableId = xe.GetAttribute(Const.Id);
            var properties = xe.ChildNodes;
            foreach (XmlNode property in properties)
            {
                CommonInfoHandle(property, table);
                switch (property.Name)
                {
                    case Const.AComment:
                        table.Comment = property.InnerText;
                        break;
                    case Const.APhysicalOptions:
                        table.PhysicalOptions = property.InnerText;
                        break;
                    case Const.ADescription:
                        table.Description = property.InnerText;
                        break;
                    case Const.CColumns:
                        InitColumns(property,table);
                        break;
                    case Const.CKeys:
                        InitKeys(property,table);
                        break;
                    case Const.CPrimaryKey:
                        InitPrimaryKey(property, table);
                        break;
                }
            }

            return table;
        }

        /// <summary>
        /// 通用信息处理
        /// </summary>
        /// <typeparam name="T">PDM对象</typeparam>
        /// <param name="node">节点</param>
        /// <param name="entity">PDM实体</param>
        private void CommonInfoHandle<T>(XmlNode node, T entity) where T : PdmCommonInfo
        {
            switch (node.Name)
            {
                case Const.AObjectId:
                    entity.ObjectId = node.InnerText;
                    break;
                case Const.AName:
                    entity.Name = node.InnerText;
                    break;
                case Const.ACode:
                    entity.Code = node.InnerText;
                    break;
                case Const.ACreationDate:
                    entity.CreationDate = String2DateTime(node.InnerText);
                    break;
                case Const.ACreator:
                    entity.Creator = node.InnerText;
                    break;
                case Const.AModificationDate:
                    entity.ModificationDate = String2DateTime(node.InnerText);
                    break;
                case Const.AModifier:
                    entity.Modifier = node.InnerText;
                    break;
            }
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
            view.ViewId = xe.GetAttribute(Const.Id);
            var properties = xe.ChildNodes;
            foreach (XmlNode property in properties)
            {
                CommonInfoHandle(property, view);
                switch (property.Name)
                {
                    case Const.AComment:
                        view.Comment = property.InnerText;
                        break;
                    case Const.ADescription:
                        view.Description = property.InnerText;
                        break;
                    case Const.AViewSQLQuery:
                        view.ViewSQLQuery = property.InnerText;
                        break;
                    case Const.ATaggedSQLQuery:
                        view.TaggedSQLQuery = property.InnerText;
                        break;
                    case Const.CColumns:
                        InitColumns(property, view);
                        break;
                }
            }
            return view;
        }

        #endregion

        #region InitPackages(初始化包信息)

        /// <summary>
        /// 初始化包信息
        /// </summary>
        /// <param name="packages">包节点集合</param>
        /// <param name="pdm">PDM信息</param>
        private void InitPackages(XmlNode packages, PdmInfo pdm)
        {
            foreach (XmlNode package in packages)
                pdm.Packages.Add(GetPackage(package));
        }

        #endregion

        #region GetPackage(获取包信息)

        /// <summary>
        /// 获取包信息
        /// </summary>
        /// <param name="node">节点</param>
        private PackageInfo GetPackage(XmlNode node)
        {
            var package = new PackageInfo();
            var xe = (XmlElement)node;
            package.PackageId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                CommonInfoHandle(property, package);
            }

            return package;
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
        private KeyInfo GetKey(XmlNode node, TableInfo ownerTable)
        {
            var key = new KeyInfo(ownerTable);
            var xe = (XmlElement)node;
            key.KeyId = xe.GetAttribute(Const.Id);
            var properties = xe.ChildNodes;
            foreach (XmlNode property in properties)
            {
                CommonInfoHandle(property, key);
                switch (property.Name)
                {                    
                    case Const.CKeyColumns:
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
        private void InitKeyColumns(XmlNode node, KeyInfo key)
        {
            var xe = (XmlElement)node;
            foreach (XmlNode property in xe.ChildNodes)
            {
                key.ColumnRefIds.Add(((XmlElement) property).GetAttribute(Const.Ref));
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
                return pk.GetAttribute(Const.Ref);
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
            column.ColumnId = xe.GetAttribute(Const.Id);
            var properties = xe.ChildNodes;
            foreach (XmlNode property in properties)
            {
                CommonInfoHandle(property, column);
                switch (property.Name)
                {
                    case Const.AComment:
                        column.Comment = property.InnerText;
                        break;
                    case Const.ADataType:
                        column.DataType = property.InnerText;
                        break;
                    case Const.ALength:
                        column.Length = property.InnerText;
                        break;
                    case Const.AIdentity:
                        column.Identity = ConvertToBoolean(property.InnerText);
                        break;
                    case Const.AColumnMandatory:
                        column.Mandatory = ConvertToBoolean(property.InnerText);
                        break;
                    case Const.APhysicalOptions:
                        column.PhysicalOptions = property.InnerText;
                        break;
                    case Const.AExtendedAttributesText:
                        column.ExtendedAttributesText = property.InnerText;
                        break;
                    case Const.APrecision:
                        column.Precision = property.InnerText;
                        break;
                    case Const.ADescription:
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
            column.ViewColumnId = xe.GetAttribute(Const.Id);
            var properties = xe.ChildNodes;
            foreach (XmlNode property in properties)
            {
                CommonInfoHandle(property, column);
                switch (property.Name)
                {
                    case Const.AComment:
                        column.Comment = property.InnerText;
                        break;
                    case Const.ADataType:
                        column.DataType = property.InnerText;
                        break;
                    case Const.ALength:
                        column.Length = property.InnerText;
                        break;
                    case Const.ADescription:
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
