using System.Xml;
using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Extensions;
using Bing.Pdm.Models.Tables;

namespace Bing.Pdm.Core.Loaders
{
    /// <summary>
    /// 数据表加载器
    /// </summary>
    internal class TableLoader:ITableLoader
    {
        /// <summary>
        /// Key加载器
        /// </summary>
        private readonly IKeyLoader _keyLoader;

        /// <summary>
        /// 初始化一个<see cref="TableLoader"/>类型的实例
        /// </summary>
        /// <param name="keyLoader">Key加载器</param>
        public TableLoader(IKeyLoader keyLoader)
        {
            _keyLoader = keyLoader;
        }

        /// <summary>
        /// 获取数据表
        /// </summary>
        /// <param name="node">节点</param>
        public TableInfo GetTable(XmlNode node)
        {
            var table = new TableInfo();
            var xe = (XmlElement)node;
            table.TableId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                property.CommonInfoHandle(table);
                switch (property.Name)
                {
                    case Const.APhysicalOptions:
                        table.PhysicalOptions = property.InnerText;
                        break;
                    case Const.CColumns:
                        InitColumns(property, table);
                        break;
                    case Const.CKeys:
                        InitKeys(property, table);
                        break;
                    case Const.CPrimaryKey:
                        InitPrimaryKey(property, table);
                        break;
                }
            }

            return table;
        }

        /// <summary>
        /// 初始化数据表列
        /// </summary>
        /// <param name="columns">列节点集合</param>
        /// <param name="table">数据表</param>
        private void InitColumns(XmlNode columns, TableInfo table)
        {
            foreach (XmlNode column in columns)
            {
                table.AddColumn(GetColumn(column, table));
            }
        }

        /// <summary>
        /// 获取数据表列
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="ownerTable">所有者数据表</param>
        private ColumnInfo GetColumn(XmlNode node, TableInfo ownerTable)
        {
            var column = new ColumnInfo(ownerTable);
            var xe = (XmlElement)node;
            column.ColumnId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                property.CommonInfoHandle(column);
                switch (property.Name)
                {
                    case Const.ADataType:
                        column.DataType = property.InnerText;
                        break;
                    case Const.ALength:
                        column.Length = property.InnerText;
                        break;
                    case Const.AIdentity:
                        column.Identity = property.InnerText.ToBoolean();
                        break;
                    case Const.AColumnMandatory:
                        column.Mandatory = property.InnerText.ToBoolean();
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
                }
            }

            return column;
        }

        /// <summary>
        /// 初始化Keys
        /// </summary>
        /// <param name="keys">Key节点集合</param>
        /// <param name="table">数据表</param>
        private void InitKeys(XmlNode keys, TableInfo table)
        {
            foreach (XmlNode key in keys)
            {
                table.AddKey(_keyLoader.GetKey(key, table));
            }
        }

        /// <summary>
        /// 初始化主键
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="table">所有者数据表</param>
        private void InitPrimaryKey(XmlNode node, TableInfo table)
        {
            table.PrimaryKeyRefCode = GetPrimaryKey(node);
        }

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
    }
}
