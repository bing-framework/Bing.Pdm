using System.Xml;
using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Extensions;
using Bing.Pdm.Models.Views;

namespace Bing.Pdm.Core.Loaders
{
    /// <summary>
    /// 视图加载器
    /// </summary>
    internal class ViewLoader : IViewLoader
    {
        /// <summary>
        /// 获取视图
        /// </summary>
        /// <param name="node">节点</param>
        public ViewInfo GetView(XmlNode node)
        {
            var view = new ViewInfo();
            var xe = (XmlElement)node;
            view.ViewId = xe.GetAttribute(Const.Id);
            foreach (XmlNode property in xe.ChildNodes)
            {
                property.CommonInfoHandle(view);
                switch (property.Name)
                {
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

        /// <summary>
        /// 初始化视图列
        /// </summary>
        /// <param name="columns">列节点集合</param>
        /// <param name="view">视图</param>
        private void InitColumns(XmlNode columns, ViewInfo view)
        {
            foreach (XmlNode column in columns)
            {
                view.Columns.Add(GetColumn(column, view));
            }
        }

        /// <summary>
        /// 获取视图列
        /// </summary>
        /// <param name="node">节点</param>
        /// <param name="ownerView">所有者视图</param>
        private ViewColumnInfo GetColumn(XmlNode node, ViewInfo ownerView)
        {
            var column = new ViewColumnInfo(ownerView);
            var xe = (XmlElement)node;
            column.ViewColumnId = xe.GetAttribute(Const.Id);
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
                }
            }

            return column;
        }
    }
}
