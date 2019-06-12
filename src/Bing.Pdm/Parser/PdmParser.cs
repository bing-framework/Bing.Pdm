using System;
using System.Collections.Generic;
using System.Xml;
using Bing.Pdm.Models;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Pdm.Parser
{
    public class PdmParser : IPdmParser
    {
        /// <summary>
        /// Xml命名空间管理
        /// </summary>
        private XmlNamespaceManager _xmlNsManager;

        /// <summary>
        /// PDM
        /// </summary>
        private Models.Pdm _pdm;

        /// <summary>
        /// PDM 分析
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public Models.Pdm Parser(string filePath)
        {
            XmlDocument document = new XmlDocument();
            document.Load(filePath);

            var root = document.DocumentElement;
            _xmlNsManager = GetXmlNamespaceManager(document.NameTable);
            _pdm = new Models.Pdm();

            var model = root?.SelectSingleNode("//c:Children/o:Model", _xmlNsManager);
            _pdm.Id = model?.GetAttribute("Id");
            _pdm.Name = model?.SelectSingleNode("a:Name", _xmlNsManager)?.InnerText;
            _pdm.Code= model?.SelectSingleNode("a:Code", _xmlNsManager)?.InnerText;

            var dbms = model?.SelectSingleNode("//o:Shortcut", _xmlNsManager);
            _pdm.DbCode = dbms?.SelectSingleNode("a:Code", _xmlNsManager)?.InnerText;
            _pdm.DbName = dbms?.SelectSingleNode("a:Name", _xmlNsManager)?.InnerText;

            _pdm.Users = UserParser(model);
            _pdm.Tables = TableParser(model);
            _pdm.PhysicalDiagrams = PhysicalDiagramParser(model);
            _pdm.References = ReferenceParser(model);

            return _pdm;
        }

        /// <summary>
        /// 获取Xml命名空间管理
        /// </summary>
        /// <param name="xmlNameTable">Xml名称表</param>
        private XmlNamespaceManager GetXmlNamespaceManager(XmlNameTable xmlNameTable)
        {
            var xmlnsManager = new XmlNamespaceManager(xmlNameTable);
            xmlnsManager.AddNamespace("a", "attribute");
            xmlnsManager.AddNamespace("c", "collection");
            xmlnsManager.AddNamespace("o", "object");
            return xmlnsManager;
        }

        /// <summary>
        /// PDM 物理图分析
        /// </summary>
        /// <param name="node">Xml节点</param>
        private List<PdmPhysicalDiagram> PhysicalDiagramParser(XmlNode node)
        {
            var list = new List<PdmPhysicalDiagram>();
            foreach (XmlNode physicalDiagramNode in node.SelectNodes("c:PhysicalDiagrams/o:PhysicalDiagram", _xmlNsManager))
            {
                var physicalDiagram = new PdmPhysicalDiagram();
                physicalDiagram.Id = physicalDiagramNode?.GetAttribute("Id");
                physicalDiagram.Name = physicalDiagramNode?.SelectSingleNode("a:Name", _xmlNsManager)?.InnerText;
                physicalDiagram.Code = physicalDiagramNode?.SelectSingleNode("a:Code", _xmlNsManager)?.InnerText;
                // 添加Table
                foreach (XmlNode tableNode in physicalDiagramNode.SelectNodes("c:Symbols/o:TableSymbol/c:Object/o:Table",_xmlNsManager))
                {
                    var id = tableNode.GetAttribute("Ref");
                    physicalDiagram.Tables.Add(_pdm.FindTable(id));
                }
                list.Add(physicalDiagram);
            }
            return list;
        }

        /// <summary>
        /// PDM 表结构分析
        /// </summary>
        /// <param name="node">Xml节点</param>
        private List<PdmTable> TableParser(XmlNode node)
        {
            var list = new List<PdmTable>();
            var tableNodes = new List<XmlNode>();
            var packageTableNodes = node.SelectNodes("c:Packages/o:Package/c:Tables/o:Table", _xmlNsManager);
            if (packageTableNodes.Count > 0)
            {
                for (var i = 0; i < packageTableNodes.Count; i++)
                {
                    tableNodes.Add(packageTableNodes.Item(i));
                }
            }
            var noPackageTableNodes = node.SelectNodes("c:Tables/o:Table", _xmlNsManager);
            if (noPackageTableNodes.Count > 0)
            {
                for (var i = 0; i < noPackageTableNodes.Count; i++)
                {
                    tableNodes.Add(noPackageTableNodes.Item(i));
                }
            }

            foreach (var tableNode in tableNodes)
            {
                var table = new PdmTable();
                table.Id = tableNode.GetAttribute("Id");
                table.Name = tableNode?.SelectSingleNode("a:Name", _xmlNsManager)?.InnerText;
                table.Code = tableNode?.SelectSingleNode("a:Code", _xmlNsManager)?.InnerText;
                // 添加Columns
                table.Columns = ColumnParser(tableNode);
                // 添加Key
                foreach (XmlNode keyNode in tableNode.SelectNodes("c:Keys/o:Key", _xmlNsManager))
                {
                    var key = new PdmKey();
                    key.Id = keyNode.GetAttribute("Id");
                    key.Name = keyNode?.SelectSingleNode("a:Name", _xmlNsManager)?.InnerText;
                    key.Code = keyNode?.SelectSingleNode("a:Code", _xmlNsManager)?.InnerText;
                    foreach (XmlNode columnNode in keyNode.SelectNodes("c:Key.Columns/o:Column",_xmlNsManager))
                    {
                        var id = columnNode.GetAttribute("Ref");
                        key.Columns.Add(table.FindColumn(id));
                    }
                    table.Keys.Add(key);
                }
                // 添加PrimaryKey
                if (tableNode.SelectSingleNode("c:PrimaryKey/o:Key", _xmlNsManager) != null)
                {
                    var id = tableNode.SelectSingleNode("c:PrimaryKey/o:Key", _xmlNsManager).GetAttribute("Ref");
                    table.PrimaryKey = table.FindKey(id);
                }
                // 添加Indexes
                foreach (XmlNode indexNode in tableNode.SelectNodes("c:Indexes/o:Index",_xmlNsManager))
                {
                    var index = new PdmIndex();
                    index.Id = indexNode.GetAttribute("Id");
                    index.Name = indexNode?.SelectSingleNode("a:Name", _xmlNsManager)?.InnerText;
                    index.Code = indexNode?.SelectSingleNode("a:Code", _xmlNsManager)?.InnerText;
                    table.Indexs.Add(index);
                }
                // 添加User
                var userNode = tableNode.SelectSingleNode("c:Owner/o:User", _xmlNsManager);
                if (userNode != null)
                {
                    var id = userNode.GetAttribute("Ref");
                    table.User = _pdm.FindUser(id);
                }
                list.Add(table);
            }
            return list;
        }

        /// <summary>
        /// PDM 列结构分析
        /// </summary>
        /// <param name="node">Xml节点</param>
        private List<PdmColumn> ColumnParser(XmlNode node)
        {
            var list = new List<PdmColumn>();
            foreach (XmlNode columnNode in node.SelectNodes("c:Columns/o:Column",_xmlNsManager))
            {
                var column = new PdmColumn();
                column.Id = columnNode.GetAttribute("Id");
                column.Name = columnNode?.SelectSingleNode("a:Name", _xmlNsManager)?.InnerText;
                column.Code = columnNode?.SelectSingleNode("a:Code", _xmlNsManager)?.InnerText;
                column.DataType = columnNode?.SelectSingleNode("a:DataType", _xmlNsManager)?.InnerText;
                column.Length = Conv.ToInt(columnNode?.SelectSingleNode("a:Length", _xmlNsManager)?.InnerText);
                column.Precision = Conv.ToInt(columnNode?.SelectSingleNode("a:Precision", _xmlNsManager)?.InnerText);
                column.Mandatory = Conv.ToInt(columnNode?.SelectSingleNode("a:Column.Mandatory", _xmlNsManager)?.InnerText);
                column.DefaultValue = columnNode?.SelectSingleNode("a:DefaultValue", _xmlNsManager)?.InnerText;
                column.LowValue = columnNode?.SelectSingleNode("a:LowValue", _xmlNsManager)?.InnerText;
                column.HighValue = columnNode?.SelectSingleNode("a:HighValue", _xmlNsManager)?.InnerText;
                column.Comment = columnNode?.SelectSingleNode("a:Comment", _xmlNsManager)?.InnerText;
                list.Add(column);
            }
            return list;
        }

        /// <summary>
        /// PDM 用户分析
        /// </summary>
        /// <param name="node">Xml节点</param>
        private List<PdmUser> UserParser(XmlNode node)
        {
            var list = new List<PdmUser>();
            foreach (XmlNode userNode in node.SelectNodes("c:Users/o:User",_xmlNsManager))
            {
                var user = new PdmUser
                {
                    Id = userNode?.GetAttribute("Id"),
                    Name = userNode?.SelectSingleNode("a:Name", _xmlNsManager)?.InnerText,
                    Code = userNode?.SelectSingleNode("a:Code", _xmlNsManager)?.InnerText
                };
                list.Add(user);
            }
            return list;
        }

        /// <summary>
        /// PDM 引用分析
        /// </summary>
        /// <param name="node">Xml节点</param>
        private List<PdmReference> ReferenceParser(XmlNode node)
        {
            var list = new List<PdmReference>();
            return list;
        }
    }
}
