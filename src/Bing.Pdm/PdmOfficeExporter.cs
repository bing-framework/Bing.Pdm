using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using Bing.Pdm.Models;
using Bing.Pdm.Models.Tables;

namespace Bing.Pdm
{
    /// <summary>
    /// 导出 PDM 数据字典到 Office 文档。
    /// </summary>
    public interface IPdmOfficeExporter
    {
        /// <summary>
        /// 将模型导出为 Excel 文档。
        /// </summary>
        /// <param name="model">待导出的 PDM 模型。</param>
        /// <param name="output">Excel 输出流。</param>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> 或 <paramref name="output"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException"><paramref name="output"/> 不支持写入。</exception>
        void WriteExcel(PdmInfo model, Stream output);

        /// <summary>
        /// 将模型导出为 Word 文档。
        /// </summary>
        /// <param name="model">待导出的 PDM 模型。</param>
        /// <param name="output">Word 输出流。</param>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> 或 <paramref name="output"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException"><paramref name="output"/> 不支持写入。</exception>
        void WriteWord(PdmInfo model, Stream output);
    }

    /// <summary>
    /// 生成不依赖 Office 客户端的 Excel 和 Word 文档。
    /// </summary>
    public sealed class PdmOfficeExporter : IPdmOfficeExporter
    {
        /// <summary>
        /// Excel 工作簿 XML 使用的 SpreadsheetML 命名空间。
        /// </summary>
        private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        /// <summary>
        /// Excel 工作簿 XML 使用的关系命名空间。
        /// </summary>
        private const string SpreadsheetRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        /// <summary>
        /// Office Open XML 文档包关系部件使用的命名空间。
        /// </summary>
        private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        /// <summary>
        /// Word 文档 XML 使用的 WordprocessingML 命名空间。
        /// </summary>
        private const string WordNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        /// <summary>
        /// Office Open XML 内容类型清单使用的命名空间。
        /// </summary>
        private const string ContentTypeNs = "http://schemas.openxmlformats.org/package/2006/content-types";
        /// <summary>
        /// 提供 Office 文档导出所需标签的解析器。
        /// </summary>
        private readonly PdmExporter _labels;

        /// <summary>
        /// 初始化一个 <see cref="PdmOfficeExporter"/> 类型的实例。
        /// </summary>
        public PdmOfficeExporter() : this(PdmExportLanguage.English, null) { }

        /// <summary>
        /// 初始化一个 <see cref="PdmOfficeExporter"/> 类型的实例。
        /// </summary>
        /// <param name="language">导出语言。</param>
        public PdmOfficeExporter(PdmExportLanguage language) : this(language, null) { }

        /// <summary>
        /// 初始化一个 <see cref="PdmOfficeExporter"/> 类型的实例。
        /// </summary>
        /// <param name="language">导出语言。</param>
        /// <param name="labelProvider">可选的标签提供器。</param>
        public PdmOfficeExporter(PdmExportLanguage language, IPdmExportLabelProvider labelProvider)
        {
            _labels = new PdmExporter(language, labelProvider);
        }

        /// <inheritdoc />
        public void WriteExcel(PdmInfo model, Stream output)
        {
            Validate(model, output);
            var sheets = CreateSheets(model);
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                AddXml(archive, "[Content_Types].xml", WriteWorkbookContentTypes);
                AddXml(archive, "_rels/.rels", writer => WritePackageRelationships(writer, "xl/workbook.xml"));
                AddXml(archive, "xl/workbook.xml", writer => WriteWorkbook(writer, sheets));
                AddXml(archive, "xl/_rels/workbook.xml.rels", writer => WriteWorkbookRelationships(writer, sheets.Count));
                AddXml(archive, "xl/styles.xml", WriteWorkbookStyles);
                for (var i = 0; i < sheets.Count; i++)
                {
                    var sheet = sheets[i];
                    var index = i + 1;
                    AddXml(archive, "xl/worksheets/sheet" + index.ToString(CultureInfo.InvariantCulture) + ".xml",
                        writer => WriteWorksheet(writer, sheet));
                }
            }
        }

        /// <inheritdoc />
        public void WriteWord(PdmInfo model, Stream output)
        {
            Validate(model, output);
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                AddXml(archive, "[Content_Types].xml", WriteWordContentTypes);
                AddXml(archive, "_rels/.rels", writer => WritePackageRelationships(writer, "word/document.xml"));
                AddXml(archive, "word/_rels/document.xml.rels", WriteWordRelationships);
                AddXml(archive, "word/styles.xml", WriteWordStyles);
                AddXml(archive, "word/document.xml", writer => WriteDocument(writer, model));
            }
        }

        /// <summary>
        /// 创建 Excel 工作表数据。
        /// </summary>
        /// <param name="model">待导出的 PDM 模型。</param>
        /// <returns>包含模型数据的 Excel 工作表集合。</returns>
        private List<Sheet> CreateSheets(PdmInfo model)
        {
            var sheets = new List<Sheet>();
            var tables = new Sheet(L("Tables"), L("Tables"), L("Schema"), L("Owner"), L("Code"), L("Name"), L("Description"), L("Comment"), L("PrimaryKey"));
            foreach (var item in TablesWithPaths(model))
            {
                var table = item.Item1;
                var owner = model.Owners.FirstOrDefault(x => x.Id == table.OwnerId);
                var schema = model.Schemas.FirstOrDefault(x => x.Id == table.SchemaId);
                var primary = table.Keys.FirstOrDefault(x => x.Id == table.PrimaryKeyId);
                tables.Rows.Add(new[] { table.Id, item.Item2, schema?.Name ?? table.SchemaId, owner?.Name ?? table.OwnerId,
                    table.Code, table.Name, table.Description, table.Comment, primary?.Code });
            }
            sheets.Add(tables);

            var columns = new Sheet(L("SheetColumns"), L("TableId"), L("Table"), L("ColumnId"), L("Code"), L("Name"), L("Type"), L("Length"), L("Precision"), L("Required"), L("PrimaryKey"), L("Identity"), L("Default"), L("Comment"));
            foreach (var item in TablesWithPaths(model))
                foreach (var column in item.Item1.Columns)
                    columns.Rows.Add(new[] { item.Item1.Id, item.Item1.Code, column.Id, column.Code, column.Name, column.DataType,
                        column.Length, column.Precision, YesNo(column.Mandatory), YesNo(column.PrimaryKey), YesNo(column.Identity),
                        column.DefaultValue, column.Comment ?? column.Description });
            sheets.Add(columns);

            var keys = new Sheet(L("SheetKeysIndexes"), L("TableId"), L("Table"), L("Kind"), L("Name"), L("Unique"), L("Columns"));
            foreach (var item in TablesWithPaths(model))
            {
                foreach (var key in item.Item1.Keys)
                    keys.Rows.Add(new[] { item.Item1.Id, item.Item1.Code, key.Id == item.Item1.PrimaryKeyId ? L("PrimaryKey") : L("Key"), key.Code,
                        YesNo(key.Id == item.Item1.PrimaryKeyId), ColumnCodes(item.Item1, key.ColumnIds) });
                foreach (var index in item.Item1.Indexes)
                    keys.Rows.Add(new[] { item.Item1.Id, item.Item1.Code, L("Index"), index.Code, YesNo(index.Unique), ColumnCodes(item.Item1, index.ColumnIds) });
            }
            sheets.Add(keys);

            var references = new Sheet(L("SheetReferences"), L("Id"), L("Reference"), L("Parent"), L("Child"), L("ParentKey"), L("Columns"), L("Cardinality"));
            foreach (var reference in model.AllReferences)
                references.Rows.Add(new[] { reference.Id, reference.Code, TableCode(model, reference.ParentTableId), TableCode(model, reference.ChildTableId),
                    KeyCode(model, reference.ParentKeyId), JoinText(model, reference), reference.Cardinality });
            sheets.Add(references);

            var views = new Sheet(L("SheetViews"), L("Id"), L("Package"), L("Code"), L("Name"), L("Description"), L("Comment"), "SQL", L("TaggedSql"));
            foreach (var item in ViewsWithPaths(model))
                views.Rows.Add(new[] { item.Item1.Id, item.Item2, item.Item1.Code, item.Item1.Name, item.Item1.Description,
                    item.Item1.Comment, item.Item1.ViewSQLQuery, item.Item1.TaggedSQLQuery });
            sheets.Add(views);

            var viewColumns = new Sheet(L("SheetViewColumns"), L("ViewId"), L("View"), L("ColumnId"), L("Code"), L("Name"), L("Type"), L("Length"), L("Precision"), L("Required"), L("Default"), L("Comment"));
            foreach (var item in ViewsWithPaths(model))
                foreach (var column in item.Item1.Columns)
                    viewColumns.Rows.Add(new[] { item.Item1.Id, item.Item1.Code, column.Id, column.Code, column.Name, column.DataType,
                        column.Length, column.Precision, YesNo(column.Mandatory), column.DefaultValue, column.Comment ?? column.Description });
            sheets.Add(viewColumns);

            var diagnostics = new Sheet(L("SheetDiagnostics"), L("Code"), L("SourceId"), L("Message"));
            foreach (var diagnostic in model.Diagnostics)
                diagnostics.Rows.Add(new[] { diagnostic.Code, diagnostic.SourceId, diagnostic.Message });
            sheets.Add(diagnostics);
            return sheets;
        }

        /// <summary>
        /// 写入 Word 文档主体。
        /// </summary>
        /// <param name="writer">Word 文档 XML 写入器。</param>
        /// <param name="model">待导出的 PDM 模型。</param>
        private void WriteDocument(XmlWriter writer, PdmInfo model)
        {
            writer.WriteStartElement("w", "document", WordNs);
            writer.WriteStartElement("w", "body", WordNs);
            WriteParagraph(writer, model.Name ?? model.Code ?? L("DataDictionary"), "Title");
            WriteParagraph(writer, L("Dbms") + ": " + (model.DbmsName ?? model.DbmsCode ?? L("Unknown")));

            WriteParagraph(writer, L("Tables"), "Heading1");
            foreach (var item in TablesWithPaths(model))
            {
                var table = item.Item1;
                WriteParagraph(writer, item.Item2 + DisplayName(table.Code, table.Name), "Heading2");
                if (!string.IsNullOrEmpty(table.Description)) WriteParagraph(writer, table.Description);
                if (!string.IsNullOrEmpty(table.Comment)) WriteParagraph(writer, L("Comment") + ": " + table.Comment);
                var owner = model.Owners.FirstOrDefault(x => x.Id == table.OwnerId);
                var schema = model.Schemas.FirstOrDefault(x => x.Id == table.SchemaId);
                if (schema != null) WriteParagraph(writer, L("Schema") + ": " + (schema.Name ?? schema.Code));
                else if (owner != null) WriteParagraph(writer, L("Owner") + ": " + (owner.Name ?? owner.Code));
                foreach (var key in table.Keys)
                    WriteParagraph(writer, (key.Id == table.PrimaryKeyId ? L("PrimaryKey") : L("Key")) + " " + key.Code + ": " + ColumnCodes(table, key.ColumnIds));
                foreach (var index in table.Indexes)
                    WriteParagraph(writer, L("Index") + " " + index.Code + " (" + L(index.Unique ? "Unique" : "NonUnique") + "): " + ColumnCodes(table, index.ColumnIds));
                WriteDocumentTable(writer,
                    new[] { L("Column"), L("Name"), L("Type"), L("Length"), L("Precision"), L("Required"), L("PrimaryKey"), L("Identity"), L("Default"), L("Comment") },
                    table.Columns.Select(column => new[] { column.Code, column.Name, column.DataType, column.Length, column.Precision,
                        YesNo(column.Mandatory), YesNo(column.PrimaryKey), YesNo(column.Identity), column.DefaultValue, column.Comment ?? column.Description }));
            }

            WriteParagraph(writer, L("References"), "Heading1");
            WriteDocumentTable(writer, new[] { L("Reference"), L("Parent"), L("Child"), L("ParentKey"), L("Columns"), L("Cardinality") },
                model.AllReferences.Select(reference => new[] { reference.Code, TableCode(model, reference.ParentTableId), TableCode(model, reference.ChildTableId),
                    KeyCode(model, reference.ParentKeyId), JoinText(model, reference), reference.Cardinality }));

            WriteParagraph(writer, L("Views"), "Heading1");
            foreach (var item in ViewsWithPaths(model))
            {
                var view = item.Item1;
                WriteParagraph(writer, item.Item2 + DisplayName(view.Code, view.Name), "Heading2");
                if (!string.IsNullOrEmpty(view.Description)) WriteParagraph(writer, view.Description);
                if (!string.IsNullOrEmpty(view.Comment)) WriteParagraph(writer, L("Comment") + ": " + view.Comment);
                WriteDocumentTable(writer, new[] { L("Column"), L("Name"), L("Type"), L("Length"), L("Precision"), L("Required"), L("Default"), L("Comment") },
                    view.Columns.Select(column => new[] { column.Code, column.Name, column.DataType, column.Length, column.Precision,
                        YesNo(column.Mandatory), column.DefaultValue, column.Comment ?? column.Description }));
                if (!string.IsNullOrEmpty(view.ViewSQLQuery)) WriteParagraph(writer, "SQL: " + view.ViewSQLQuery);
                if (!string.IsNullOrEmpty(view.TaggedSQLQuery)) WriteParagraph(writer, L("TaggedSql") + ": " + view.TaggedSQLQuery);
            }

            if (model.Diagnostics.Count > 0)
            {
                WriteParagraph(writer, L("Diagnostics"), "Heading1");
                WriteDocumentTable(writer, new[] { L("Code"), L("SourceId"), L("Message") },
                    model.Diagnostics.Select(diagnostic => new[] { diagnostic.Code, diagnostic.SourceId, diagnostic.Message }));
            }
            writer.WriteStartElement("w", "sectPr", WordNs);
            writer.WriteStartElement("w", "pgSz", WordNs);
            writer.WriteAttributeString("w", "w", WordNs, "16838");
            writer.WriteAttributeString("w", "h", WordNs, "11906");
            writer.WriteAttributeString("w", "orient", WordNs, "landscape");
            writer.WriteEndElement();
            writer.WriteStartElement("w", "pgMar", WordNs);
            writer.WriteAttributeString("w", "top", WordNs, "600");
            writer.WriteAttributeString("w", "right", WordNs, "600");
            writer.WriteAttributeString("w", "bottom", WordNs, "600");
            writer.WriteAttributeString("w", "left", WordNs, "600");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        /// <summary>
        /// 写入 Word 表格。
        /// </summary>
        /// <param name="writer">Word 文档 XML 写入器。</param>
        /// <param name="headers">表头文本。</param>
        /// <param name="rows">表格行数据。</param>
        private static void WriteDocumentTable(XmlWriter writer, string[] headers, IEnumerable<string[]> rows)
        {
            writer.WriteStartElement("w", "tbl", WordNs);
            writer.WriteStartElement("w", "tblPr", WordNs);
            writer.WriteStartElement("w", "tblStyle", WordNs);
            writer.WriteAttributeString("w", "val", WordNs, "TableGrid");
            writer.WriteEndElement();
            writer.WriteStartElement("w", "tblW", WordNs);
            writer.WriteAttributeString("w", "w", WordNs, "5000");
            writer.WriteAttributeString("w", "type", WordNs, "pct");
            writer.WriteEndElement();
            writer.WriteStartElement("w", "tblCellMar", WordNs);
            WriteWordCellMargin(writer, "top", "40");
            WriteWordCellMargin(writer, "left", "80");
            WriteWordCellMargin(writer, "bottom", "40");
            WriteWordCellMargin(writer, "right", "80");
            writer.WriteEndElement();
            writer.WriteEndElement();
            WriteDocumentRow(writer, headers, true);
            foreach (var row in rows) WriteDocumentRow(writer, row, false);
            writer.WriteEndElement();
        }

        /// <summary>
        /// 写入 Word 表格单元格边距。
        /// </summary>
        /// <param name="writer">Word 文档 XML 写入器。</param>
        /// <param name="side">边距所在方向。</param>
        /// <param name="width">边距宽度。</param>
        private static void WriteWordCellMargin(XmlWriter writer, string side, string width)
        {
            writer.WriteStartElement("w", side, WordNs);
            writer.WriteAttributeString("w", "w", WordNs, width);
            writer.WriteAttributeString("w", "type", WordNs, "dxa");
            writer.WriteEndElement();
        }

        /// <summary>
        /// 写入 Word 表格行。
        /// </summary>
        /// <param name="writer">Word 文档 XML 写入器。</param>
        /// <param name="values">行内单元格文本。</param>
        /// <param name="header">是否按表头样式写入。</param>
        private static void WriteDocumentRow(XmlWriter writer, IEnumerable<string> values, bool header)
        {
            writer.WriteStartElement("w", "tr", WordNs);
            if (header)
            {
                writer.WriteStartElement("w", "trPr", WordNs);
                writer.WriteStartElement("w", "tblHeader", WordNs);
                writer.WriteAttributeString("w", "val", WordNs, "true");
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            foreach (var value in values)
            {
                writer.WriteStartElement("w", "tc", WordNs);
                writer.WriteStartElement("w", "tcPr", WordNs);
                if (header)
                {
                    writer.WriteStartElement("w", "shd", WordNs);
                    writer.WriteAttributeString("w", "fill", WordNs, "DFEDEF");
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                WriteParagraph(writer, value, null, header);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        /// <summary>
        /// 写入 Word 段落。
        /// </summary>
        /// <param name="writer">Word 文档 XML 写入器。</param>
        /// <param name="value">段落文本。</param>
        /// <param name="style">可选的段落样式标识。</param>
        /// <param name="bold">是否以粗体写入文本。</param>
        private static void WriteParagraph(XmlWriter writer, string value, string style = null, bool bold = false)
        {
            writer.WriteStartElement("w", "p", WordNs);
            if (!string.IsNullOrEmpty(style))
            {
                writer.WriteStartElement("w", "pPr", WordNs);
                writer.WriteStartElement("w", "pStyle", WordNs);
                writer.WriteAttributeString("w", "val", WordNs, style);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteStartElement("w", "r", WordNs);
            if (bold)
            {
                writer.WriteStartElement("w", "rPr", WordNs);
                writer.WriteStartElement("w", "b", WordNs);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteStartElement("w", "t", WordNs);
            writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
            writer.WriteString(value ?? string.Empty);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        /// <summary>
        /// 写入 Excel 工作表 XML。
        /// </summary>
        /// <param name="writer">工作表 XML 写入器。</param>
        /// <param name="sheet">待写入的工作表数据。</param>
        private void WriteWorksheet(XmlWriter writer, Sheet sheet)
        {
            writer.WriteStartElement("worksheet", SpreadsheetNs);
            writer.WriteStartElement("sheetPr", SpreadsheetNs);
            writer.WriteStartElement("pageSetUpPr", SpreadsheetNs);
            writer.WriteAttributeString("fitToPage", "1");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteStartElement("sheetViews", SpreadsheetNs);
            writer.WriteStartElement("sheetView", SpreadsheetNs);
            writer.WriteAttributeString("workbookViewId", "0");
            writer.WriteStartElement("pane", SpreadsheetNs);
            writer.WriteAttributeString("ySplit", "1");
            writer.WriteAttributeString("topLeftCell", "A2");
            writer.WriteAttributeString("activePane", "bottomLeft");
            writer.WriteAttributeString("state", "frozen");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteStartElement("sheetFormatPr", SpreadsheetNs);
            writer.WriteAttributeString("defaultRowHeight", "18");
            writer.WriteEndElement();
            writer.WriteStartElement("cols", SpreadsheetNs);
            var widths = GetColumnWidths(sheet);
            for (var i = 0; i < widths.Length; i++)
            {
                writer.WriteStartElement("col", SpreadsheetNs);
                writer.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("width", widths[i].ToString("0.##", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("customWidth", "1");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteStartElement("sheetData", SpreadsheetNs);
            WriteWorksheetRow(writer, sheet.Headers, 1, true);
            for (var i = 0; i < sheet.Rows.Count; i++) WriteWorksheetRow(writer, sheet.Rows[i], i + 2, false);
            writer.WriteEndElement();
            writer.WriteStartElement("autoFilter", SpreadsheetNs);
            writer.WriteAttributeString("ref", "A1:" + ColumnName(sheet.Headers.Length) + Math.Max(1, sheet.Rows.Count + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
            writer.WriteStartElement("pageMargins", SpreadsheetNs);
            writer.WriteAttributeString("left", "0.3");
            writer.WriteAttributeString("right", "0.3");
            writer.WriteAttributeString("top", "0.5");
            writer.WriteAttributeString("bottom", "0.5");
            writer.WriteAttributeString("header", "0.2");
            writer.WriteAttributeString("footer", "0.2");
            writer.WriteEndElement();
            writer.WriteStartElement("pageSetup", SpreadsheetNs);
            writer.WriteAttributeString("paperSize", "9");
            writer.WriteAttributeString("orientation", "landscape");
            writer.WriteAttributeString("fitToWidth", "1");
            writer.WriteAttributeString("fitToHeight", "0");
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        /// <summary>
        /// 写入 Excel 工作表行。
        /// </summary>
        /// <param name="writer">工作表 XML 写入器。</param>
        /// <param name="values">行内单元格文本。</param>
        /// <param name="rowNumber">从一开始计数的行号。</param>
        /// <param name="header">是否按表头样式写入。</param>
        private static void WriteWorksheetRow(XmlWriter writer, string[] values, int rowNumber, bool header)
        {
            writer.WriteStartElement("row", SpreadsheetNs);
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
            for (var column = 0; column < values.Length; column++)
            {
                writer.WriteStartElement("c", SpreadsheetNs);
                writer.WriteAttributeString("r", ColumnName(column + 1) + rowNumber.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("t", "inlineStr");
                writer.WriteAttributeString("s", header ? "1" : "2");
                writer.WriteStartElement("is", SpreadsheetNs);
                writer.WriteStartElement("t", SpreadsheetNs);
                writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                writer.WriteString(values[column] ?? string.Empty);
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        /// <summary>
        /// 计算工作表各列的显示宽度。
        /// </summary>
        /// <param name="sheet">待测量的工作表。</param>
        /// <returns>按表头和单元格文本计算的列宽数组。</returns>
        private static double[] GetColumnWidths(Sheet sheet)
        {
            const double minimumWidth = 9;
            const double maximumWidth = 36;
            const double maximumTotalWidth = 150;
            var widths = new double[sheet.Headers.Length];
            for (var column = 0; column < widths.Length; column++)
            {
                var contentWidth = DisplayWidth(sheet.Headers[column]);
                foreach (var row in sheet.Rows)
                    if (column < row.Length)
                        contentWidth = Math.Max(contentWidth, DisplayWidth(row[column]));
                widths[column] = Math.Max(minimumWidth, Math.Min(maximumWidth, contentWidth + 2));
            }

            var totalWidth = widths.Sum();
            if (totalWidth > maximumTotalWidth && widths.Length > 0)
            {
                var remainingWidth = maximumTotalWidth - minimumWidth * widths.Length;
                var excessWidth = totalWidth - minimumWidth * widths.Length;
                var scale = remainingWidth / excessWidth;
                for (var i = 0; i < widths.Length; i++)
                    widths[i] = minimumWidth + (widths[i] - minimumWidth) * scale;
            }
            return widths;
        }

        /// <summary>
        /// 计算文本的显示宽度。
        /// </summary>
        /// <param name="value">待计算的文本。</param>
        /// <returns>按 ASCII 字符宽度为一、其他字符宽度为二计算的宽度。</returns>
        private static int DisplayWidth(string value)
        {
            var width = 0;
            foreach (var character in value ?? string.Empty)
                width += character > 0xff ? 2 : 1;
            return width;
        }

        /// <summary>
        /// 将列序号转换为 Excel 列名。
        /// </summary>
        /// <param name="number">从一开始计数的列序号。</param>
        /// <returns>对应的 Excel 列名；小于等于零时返回空字符串。</returns>
        private static string ColumnName(int number)
        {
            var name = string.Empty;
            while (number > 0)
            {
                number--;
                name = (char)('A' + number % 26) + name;
                number /= 26;
            }
            return name;
        }

        /// <summary>
        /// 写入 Excel 工作簿清单。
        /// </summary>
        /// <param name="writer">工作簿 XML 写入器。</param>
        /// <param name="sheets">工作簿中的工作表集合。</param>
        private static void WriteWorkbook(XmlWriter writer, IList<Sheet> sheets)
        {
            writer.WriteStartElement("workbook", SpreadsheetNs);
            writer.WriteAttributeString("xmlns", "r", null, SpreadsheetRelNs);
            writer.WriteStartElement("sheets", SpreadsheetNs);
            for (var i = 0; i < sheets.Count; i++)
            {
                writer.WriteStartElement("sheet", SpreadsheetNs);
                writer.WriteAttributeString("name", sheets[i].Name);
                writer.WriteAttributeString("sheetId", (i + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("r", "id", SpreadsheetRelNs, "rId" + (i + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        /// <summary>
        /// 写入 Excel 工作簿关系。
        /// </summary>
        /// <param name="writer">关系部件 XML 写入器。</param>
        /// <param name="sheetCount">工作簿中的工作表数量。</param>
        private static void WriteWorkbookRelationships(XmlWriter writer, int sheetCount)
        {
            writer.WriteStartElement("Relationships", PackageRelNs);
            for (var i = 1; i <= sheetCount; i++)
            {
                writer.WriteStartElement("Relationship", PackageRelNs);
                writer.WriteAttributeString("Id", "rId" + i.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("Type", SpreadsheetRelNs + "/worksheet");
                writer.WriteAttributeString("Target", "worksheets/sheet" + i.ToString(CultureInfo.InvariantCulture) + ".xml");
                writer.WriteEndElement();
            }
            writer.WriteStartElement("Relationship", PackageRelNs);
            writer.WriteAttributeString("Id", "rId" + (sheetCount + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("Type", SpreadsheetRelNs + "/styles");
            writer.WriteAttributeString("Target", "styles.xml");
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        /// <summary>
        /// 写入 Excel 样式定义。
        /// </summary>
        /// <param name="writer">样式部件 XML 写入器。</param>
        private static void WriteWorkbookStyles(XmlWriter writer)
        {
            writer.WriteStartElement("styleSheet", SpreadsheetNs);
            writer.WriteStartElement("fonts", SpreadsheetNs); writer.WriteAttributeString("count", "2");
            writer.WriteStartElement("font", SpreadsheetNs); writer.WriteStartElement("sz", SpreadsheetNs); writer.WriteAttributeString("val", "11"); writer.WriteEndElement(); writer.WriteStartElement("name", SpreadsheetNs); writer.WriteAttributeString("val", "Calibri"); writer.WriteEndElement(); writer.WriteEndElement();
            writer.WriteStartElement("font", SpreadsheetNs); writer.WriteStartElement("b", SpreadsheetNs); writer.WriteEndElement(); writer.WriteStartElement("sz", SpreadsheetNs); writer.WriteAttributeString("val", "11"); writer.WriteEndElement(); writer.WriteStartElement("name", SpreadsheetNs); writer.WriteAttributeString("val", "Calibri"); writer.WriteEndElement(); writer.WriteEndElement(); writer.WriteEndElement();
            writer.WriteStartElement("fills", SpreadsheetNs); writer.WriteAttributeString("count", "3");
            WriteFill(writer, "none", null); WriteFill(writer, "gray125", null); WriteFill(writer, "solid", "FFDFEDEF"); writer.WriteEndElement();
            writer.WriteStartElement("borders", SpreadsheetNs); writer.WriteAttributeString("count", "2");
            writer.WriteStartElement("border", SpreadsheetNs); WriteBorderEdges(writer, false); writer.WriteEndElement();
            writer.WriteStartElement("border", SpreadsheetNs); WriteBorderEdges(writer, true); writer.WriteEndElement(); writer.WriteEndElement();
            writer.WriteStartElement("cellStyleXfs", SpreadsheetNs); writer.WriteAttributeString("count", "1"); writer.WriteStartElement("xf", SpreadsheetNs); writer.WriteAttributeString("numFmtId", "0"); writer.WriteAttributeString("fontId", "0"); writer.WriteAttributeString("fillId", "0"); writer.WriteAttributeString("borderId", "0"); writer.WriteEndElement(); writer.WriteEndElement();
            writer.WriteStartElement("cellXfs", SpreadsheetNs); writer.WriteAttributeString("count", "3");
            WriteCellFormat(writer, "0", "0", "0", false);
            WriteCellFormat(writer, "1", "2", "1", true);
            WriteCellFormat(writer, "0", "0", "1", true);
            writer.WriteEndElement();
            writer.WriteStartElement("cellStyles", SpreadsheetNs); writer.WriteAttributeString("count", "1"); writer.WriteStartElement("cellStyle", SpreadsheetNs); writer.WriteAttributeString("name", "Normal"); writer.WriteAttributeString("xfId", "0"); writer.WriteAttributeString("builtinId", "0"); writer.WriteEndElement(); writer.WriteEndElement();
            writer.WriteEndElement();
        }

        /// <summary>
        /// 写入单元格填充样式。
        /// </summary>
        /// <param name="writer">样式部件 XML 写入器。</param>
        /// <param name="pattern">填充图案类型。</param>
        /// <param name="color">可选的填充颜色。</param>
        private static void WriteFill(XmlWriter writer, string pattern, string color)
        {
            writer.WriteStartElement("fill", SpreadsheetNs); writer.WriteStartElement("patternFill", SpreadsheetNs); writer.WriteAttributeString("patternType", pattern);
            if (color != null) { writer.WriteStartElement("fgColor", SpreadsheetNs); writer.WriteAttributeString("rgb", color); writer.WriteEndElement(); writer.WriteStartElement("bgColor", SpreadsheetNs); writer.WriteAttributeString("indexed", "64"); writer.WriteEndElement(); }
            writer.WriteEndElement(); writer.WriteEndElement();
        }

        /// <summary>
        /// 写入单元格边框定义。
        /// </summary>
        /// <param name="writer">样式部件 XML 写入器。</param>
        /// <param name="enabled">是否为单元格边缘写入边框样式。</param>
        private static void WriteBorderEdges(XmlWriter writer, bool enabled)
        {
            foreach (var edge in new[] { "left", "right", "top", "bottom", "diagonal" })
            {
                writer.WriteStartElement(edge, SpreadsheetNs);
                if (enabled && edge != "diagonal")
                {
                    writer.WriteAttributeString("style", "thin");
                    writer.WriteStartElement("color", SpreadsheetNs); writer.WriteAttributeString("rgb", "FFD9E0E2"); writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
        }

        /// <summary>
        /// 写入单元格格式定义。
        /// </summary>
        /// <param name="writer">样式部件 XML 写入器。</param>
        /// <param name="font">字体样式索引。</param>
        /// <param name="fill">填充样式索引。</param>
        /// <param name="border">边框样式索引。</param>
        /// <param name="wrapText">是否启用自动换行。</param>
        private static void WriteCellFormat(XmlWriter writer, string font, string fill, string border, bool wrapText)
        {
            writer.WriteStartElement("xf", SpreadsheetNs);
            writer.WriteAttributeString("numFmtId", "0"); writer.WriteAttributeString("fontId", font);
            writer.WriteAttributeString("fillId", fill); writer.WriteAttributeString("borderId", border); writer.WriteAttributeString("xfId", "0");
            if (font == "1") writer.WriteAttributeString("applyFont", "1");
            if (fill != "0") writer.WriteAttributeString("applyFill", "1");
            if (border != "0") writer.WriteAttributeString("applyBorder", "1");
            if (wrapText)
            {
                writer.WriteAttributeString("applyAlignment", "1");
                writer.WriteStartElement("alignment", SpreadsheetNs);
                writer.WriteAttributeString("vertical", "top");
                writer.WriteAttributeString("wrapText", "1");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        /// <summary>
        /// 写入 Word 样式定义。
        /// </summary>
        /// <param name="writer">Word 样式部件 XML 写入器。</param>
        private static void WriteWordStyles(XmlWriter writer)
        {
            writer.WriteStartElement("w", "styles", WordNs);
            WriteWordStyle(writer, "Normal", "paragraph", "Normal", "Calibri", "22", false, null);
            WriteWordStyle(writer, "Title", "paragraph", "Title", "Calibri", "36", true, "263B3F");
            WriteWordStyle(writer, "Heading1", "paragraph", "Heading 1", "Calibri", "30", true, "263B3F");
            WriteWordStyle(writer, "Heading2", "paragraph", "Heading 2", "Calibri", "24", true, "126B73");
            writer.WriteStartElement("w", "style", WordNs); writer.WriteAttributeString("w", "type", WordNs, "table"); writer.WriteAttributeString("w", "styleId", WordNs, "TableGrid");
            writer.WriteStartElement("w", "name", WordNs); writer.WriteAttributeString("w", "val", WordNs, "Table Grid"); writer.WriteEndElement();
            writer.WriteStartElement("w", "tblPr", WordNs); writer.WriteStartElement("w", "tblBorders", WordNs);
            foreach (var edge in new[] { "top", "left", "bottom", "right", "insideH", "insideV" })
            {
                writer.WriteStartElement("w", edge, WordNs); writer.WriteAttributeString("w", "val", WordNs, "single"); writer.WriteAttributeString("w", "sz", WordNs, "4"); writer.WriteAttributeString("w", "color", WordNs, "D9E0E2"); writer.WriteEndElement();
            }
            writer.WriteEndElement(); writer.WriteEndElement(); writer.WriteEndElement();
            writer.WriteEndElement();
        }

        /// <summary>
        /// 写入单个 Word 样式定义。
        /// </summary>
        /// <param name="writer">Word 样式部件 XML 写入器。</param>
        /// <param name="id">样式标识。</param>
        /// <param name="type">样式类型。</param>
        /// <param name="name">样式名称。</param>
        /// <param name="font">字体名称。</param>
        /// <param name="size">字号值。</param>
        /// <param name="bold">是否使用粗体。</param>
        /// <param name="color">可选的文字颜色。</param>
        private static void WriteWordStyle(XmlWriter writer, string id, string type, string name, string font, string size, bool bold, string color)
        {
            writer.WriteStartElement("w", "style", WordNs); writer.WriteAttributeString("w", "type", WordNs, type); writer.WriteAttributeString("w", "styleId", WordNs, id);
            if (id == "Normal") writer.WriteAttributeString("w", "default", WordNs, "1");
            writer.WriteStartElement("w", "name", WordNs); writer.WriteAttributeString("w", "val", WordNs, name); writer.WriteEndElement();
            writer.WriteStartElement("w", "rPr", WordNs); writer.WriteStartElement("w", "rFonts", WordNs); writer.WriteAttributeString("w", "ascii", WordNs, font); writer.WriteAttributeString("w", "hAnsi", WordNs, font); writer.WriteEndElement();
            writer.WriteStartElement("w", "sz", WordNs); writer.WriteAttributeString("w", "val", WordNs, size); writer.WriteEndElement();
            if (bold) writer.WriteStartElement("w", "b", WordNs); if (bold) writer.WriteEndElement();
            if (color != null) { writer.WriteStartElement("w", "color", WordNs); writer.WriteAttributeString("w", "val", WordNs, color); writer.WriteEndElement(); }
            writer.WriteEndElement(); writer.WriteEndElement();
        }

        /// <summary>
        /// 写入 Office 文档包的根关系。
        /// </summary>
        /// <param name="writer">关系部件 XML 写入器。</param>
        /// <param name="target">主文档部件路径。</param>
        private static void WritePackageRelationships(XmlWriter writer, string target)
        {
            writer.WriteStartElement("Relationships", PackageRelNs);
            writer.WriteStartElement("Relationship", PackageRelNs); writer.WriteAttributeString("Id", "rId1");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
            writer.WriteAttributeString("Target", target); writer.WriteEndElement(); writer.WriteEndElement();
        }

        /// <summary>
        /// 写入 Word 文档关系。
        /// </summary>
        /// <param name="writer">关系部件 XML 写入器。</param>
        private static void WriteWordRelationships(XmlWriter writer)
        {
            writer.WriteStartElement("Relationships", PackageRelNs);
            writer.WriteStartElement("Relationship", PackageRelNs); writer.WriteAttributeString("Id", "rId1");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles");
            writer.WriteAttributeString("Target", "styles.xml"); writer.WriteEndElement(); writer.WriteEndElement();
        }

        /// <summary>
        /// 写入 Excel 文档的内容类型清单。
        /// </summary>
        /// <param name="writer">内容类型清单 XML 写入器。</param>
        private static void WriteWorkbookContentTypes(XmlWriter writer)
        {
            writer.WriteStartElement("Types", ContentTypeNs);
            WriteDefaultType(writer, "rels", "application/vnd.openxmlformats-package.relationships+xml");
            WriteDefaultType(writer, "xml", "application/xml");
            WriteOverride(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
            WriteOverride(writer, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
            for (var i = 1; i <= 7; i++) WriteOverride(writer, "/xl/worksheets/sheet" + i.ToString(CultureInfo.InvariantCulture) + ".xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            writer.WriteEndElement();
        }

        /// <summary>
        /// 写入 Word 文档的内容类型清单。
        /// </summary>
        /// <param name="writer">内容类型清单 XML 写入器。</param>
        private static void WriteWordContentTypes(XmlWriter writer)
        {
            writer.WriteStartElement("Types", ContentTypeNs);
            WriteDefaultType(writer, "rels", "application/vnd.openxmlformats-package.relationships+xml");
            WriteDefaultType(writer, "xml", "application/xml");
            WriteOverride(writer, "/word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml");
            WriteOverride(writer, "/word/styles.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml");
            writer.WriteEndElement();
        }

        /// <summary>
        /// 写入默认扩展名的内容类型映射。
        /// </summary>
        /// <param name="writer">内容类型清单 XML 写入器。</param>
        /// <param name="extension">文件扩展名。</param>
        /// <param name="contentType">扩展名对应的 MIME 内容类型。</param>
        private static void WriteDefaultType(XmlWriter writer, string extension, string contentType)
        {
            writer.WriteStartElement("Default", ContentTypeNs); writer.WriteAttributeString("Extension", extension); writer.WriteAttributeString("ContentType", contentType); writer.WriteEndElement();
        }

        /// <summary>
        /// 写入指定文档部件的内容类型映射。
        /// </summary>
        /// <param name="writer">内容类型清单 XML 写入器。</param>
        /// <param name="partName">文档部件路径。</param>
        /// <param name="contentType">文档部件的 MIME 内容类型。</param>
        private static void WriteOverride(XmlWriter writer, string partName, string contentType)
        {
            writer.WriteStartElement("Override", ContentTypeNs); writer.WriteAttributeString("PartName", partName); writer.WriteAttributeString("ContentType", contentType); writer.WriteEndElement();
        }

        /// <summary>
        /// 将 XML 部件写入 Office 文档压缩包。
        /// </summary>
        /// <param name="archive">目标 Office 文档压缩包。</param>
        /// <param name="path">压缩包内的部件路径。</param>
        /// <param name="write">生成部件 XML 的写入操作。</param>
        private static void AddXml(ZipArchive archive, string path, Action<XmlWriter> write)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false }))
                write(writer);
        }

        /// <summary>
        /// 获取当前语言的标签。
        /// </summary>
        /// <param name="key">标签键。</param>
        /// <returns>标签文本；键为空时可能返回 <see langword="null"/>。</returns>
        private string L(string key) => _labels.Label(key);
        /// <summary>
        /// 将布尔值转换为当前语言的标签。
        /// </summary>
        /// <param name="value">待转换的布尔值。</param>
        /// <returns>对应的“是”或“否”标签。</returns>
        private string YesNo(bool value) => L(value ? "Yes" : "No");

        /// <summary>
        /// 按标识获取表代码。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="id">表标识。</param>
        /// <returns>表代码或名称；表未找到时返回标识，标识为空时返回空字符串。</returns>
        private static string TableCode(PdmInfo model, string id)
        {
            TableInfo table;
            return model.Lookup != null && model.Lookup.TryGetTable(id, out table) ? table.Code ?? table.Name ?? id : id ?? string.Empty;
        }

        /// <summary>
        /// 按标识获取键代码。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="id">键标识。</param>
        /// <returns>键代码或名称；键未找到时返回标识，标识为空时返回空字符串。</returns>
        private static string KeyCode(PdmInfo model, string id)
        {
            Bing.Pdm.Models.Keys.KeyInfo key;
            return model.Lookup != null && model.Lookup.TryGetKey(id, out key) ? key.Code ?? key.Name ?? id : id ?? string.Empty;
        }

        /// <summary>
        /// 格式化引用的列关联。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="reference">待格式化的引用。</param>
        /// <returns>按父列到子列格式连接的关联文本。</returns>
        private static string JoinText(PdmInfo model, Bing.Pdm.Models.References.ReferenceInfo reference)
        {
            return string.Join(", ", reference.Joins.Select(join => ColumnCode(model, join.ParentColumnId) + " -> " + ColumnCode(model, join.ChildColumnId)));
        }

        /// <summary>
        /// 按标识获取列代码。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="id">列标识。</param>
        /// <returns>列代码或名称；列未找到时返回标识，标识为空时返回空字符串。</returns>
        private static string ColumnCode(PdmInfo model, string id)
        {
            Bing.Pdm.Models.Tables.ColumnInfo column;
            return model.Lookup != null && model.Lookup.TryGetColumn(id, out column) ? column.Code ?? column.Name ?? id : id ?? string.Empty;
        }

        /// <summary>
        /// 按标识获取表列代码列表。
        /// </summary>
        /// <param name="table">包含列的表。</param>
        /// <param name="ids">列标识集合。</param>
        /// <returns>按标识顺序连接的列代码；未找到的列使用其标识。</returns>
        private static string ColumnCodes(TableInfo table, IEnumerable<string> ids) =>
            string.Join(", ", ids.Select(id => table.Columns.FirstOrDefault(x => x.Id == id)?.Code ?? id ?? string.Empty));

        /// <summary>
        /// 组合对象代码和名称。
        /// </summary>
        /// <param name="code">对象代码。</param>
        /// <param name="name">对象名称。</param>
        /// <returns>代码、名称或组合文本；代码和名称均为 <see langword="null"/> 时返回 <see langword="null"/>。</returns>
        private static string DisplayName(string code, string name) =>
            string.IsNullOrEmpty(code) || string.Equals(code, name, StringComparison.Ordinal) ? code ?? name : code + " (" + name + ")";

        /// <summary>
        /// 枚举指定范围内的表及其包路径。
        /// </summary>
        /// <param name="model">待枚举的 PDM 模型。</param>
        /// <returns>按模型顺序枚举的表及其包路径；顶层表的路径为空。</returns>
        private static IEnumerable<Tuple<TableInfo, string>> TablesWithPaths(PdmInfo model)
        {
            foreach (var table in model.Tables) yield return Tuple.Create(table, string.Empty);
            foreach (var package in model.Packages)
                foreach (var item in TablesWithPaths(package, package.Name ?? package.Code ?? package.Id)) yield return item;
        }

        /// <summary>
        /// 枚举指定范围内的表及其包路径。
        /// </summary>
        /// <param name="package">待枚举的包。</param>
        /// <param name="path">当前包的路径文本。</param>
        /// <returns>按包及其嵌套包顺序枚举的表和完整包路径。</returns>
        private static IEnumerable<Tuple<TableInfo, string>> TablesWithPaths(PackageInfo package, string path)
        {
            foreach (var table in package.Tables) yield return Tuple.Create(table, path);
            foreach (var child in package.Packages)
                foreach (var item in TablesWithPaths(child, path + " / " + (child.Name ?? child.Code ?? child.Id))) yield return item;
        }

        /// <summary>
        /// 枚举指定范围内的视图及其包路径。
        /// </summary>
        /// <param name="model">待枚举的 PDM 模型。</param>
        /// <returns>按模型顺序枚举的视图及其包路径；顶层视图的路径为空。</returns>
        private static IEnumerable<Tuple<Bing.Pdm.Models.Views.ViewInfo, string>> ViewsWithPaths(PdmInfo model)
        {
            foreach (var view in model.Views) yield return Tuple.Create(view, string.Empty);
            foreach (var package in model.Packages)
                foreach (var item in ViewsWithPaths(package, package.Name ?? package.Code ?? package.Id)) yield return item;
        }

        /// <summary>
        /// 枚举指定范围内的视图及其包路径。
        /// </summary>
        /// <param name="package">待枚举的包。</param>
        /// <param name="path">当前包的路径文本。</param>
        /// <returns>按包及其嵌套包顺序枚举的视图和完整包路径。</returns>
        private static IEnumerable<Tuple<Bing.Pdm.Models.Views.ViewInfo, string>> ViewsWithPaths(PackageInfo package, string path)
        {
            foreach (var view in package.Views) yield return Tuple.Create(view, path);
            foreach (var child in package.Packages)
                foreach (var item in ViewsWithPaths(child, path + " / " + (child.Name ?? child.Code ?? child.Id))) yield return item;
        }

        /// <summary>
        /// 验证导出模型和输出流。
        /// </summary>
        /// <param name="model">待导出的 PDM 模型。</param>
        /// <param name="output">文档输出流。</param>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> 或 <paramref name="output"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException"><paramref name="output"/> 不支持写入。</exception>
        private static void Validate(PdmInfo model, Stream output)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (!output.CanWrite) throw new ArgumentException("The output stream must be writable.", nameof(output));
        }

        /// <summary>
        /// Office 工作表的名称、表头和行数据。
        /// </summary>
        private sealed class Sheet
        {
            /// <summary>
            /// 获取工作表名称。
            /// </summary>
            public string Name { get; }
            /// <summary>
            /// 获取工作表表头。
            /// </summary>
            public string[] Headers { get; }
            /// <summary>
            /// 获取工作表行数据。
            /// </summary>
            public List<string[]> Rows { get; } = new List<string[]>();

            /// <summary>
            /// 初始化一个 <see cref="Sheet"/> 类型的实例。
            /// </summary>
            /// <param name="name">工作表名称。</param>
            /// <param name="headers">工作表表头。</param>
            public Sheet(string name, params string[] headers)
            {
                Name = name;
                Headers = headers;
            }
        }
    }
}
