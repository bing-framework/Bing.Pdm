using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Bing.Pdm.Models;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bing.Pdm
{
    /// <summary>
    /// PDM 导出格式。
    /// </summary>
    public enum PdmExportFormat
    {
        /// <summary>
        /// JSON 数据。
        /// </summary>
        Json,
        /// <summary>
        /// Markdown 数据字典。
        /// </summary>
        Markdown,
        /// <summary>
        /// HTML 数据字典。
        /// </summary>
        Html,
        /// <summary>
        /// SVG 物理图。
        /// </summary>
        Svg
    }

    /// <summary>
    /// PDM 导出语言。
    /// </summary>
    public enum PdmExportLanguage
    {
        /// <summary>
        /// 英文。
        /// </summary>
        English,
        /// <summary>
        /// 中文。
        /// </summary>
        Chinese
    }

    /// <summary>
    /// 导出 PDM 模型内容。
    /// </summary>
    public interface IPdmExporter
    {
        /// <summary>
        /// 将模型导出为指定格式。
        /// </summary>
        /// <param name="model">待导出的 PDM 模型。</param>
        /// <param name="format">导出格式。</param>
        /// <param name="writer">文本输出。</param>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> 或 <paramref name="writer"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="InvalidOperationException">导出 SVG 时模型不恰好包含一个物理图。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> 不是受支持的格式。</exception>
        void Write(PdmInfo model, PdmExportFormat format, TextWriter writer);

        /// <summary>
        /// 将指定物理图导出为 SVG。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="diagram">待导出的物理图。</param>
        /// <param name="writer">文本输出。</param>
        /// <exception cref="ArgumentNullException"><paramref name="model"/>、<paramref name="diagram"/> 或 <paramref name="writer"/> 为 <see langword="null"/>。</exception>
        void WriteDiagram(PdmInfo model, Bing.Pdm.Models.PhysicalDiagrams.PhysicalDiagramInfo diagram, TextWriter writer);
    }

    /// <summary>
    /// 导出 PDM 数据字典和物理图。
    /// </summary>
    /// <remarks>
    /// Markdown 和 HTML 格式可注册自定义模板；JSON 和 SVG 使用内置实现。
    /// </remarks>
    public sealed class PdmExporter : IPdmExporter, IPdmExportLabelProvider
    {
        /// <summary>
        /// 当前实例使用的内置标签语言。
        /// </summary>
        private readonly PdmExportLanguage _language;
        /// <summary>
        /// 可选的外部标签提供器，在内置标签前优先查询。
        /// </summary>
        private readonly IPdmExportLabelProvider _labelProvider;
        /// <summary>
        /// 当前实例按导出格式注册的自定义模板。
        /// </summary>
        private readonly Dictionary<PdmExportFormat, IPdmExportTemplate> _templates = new Dictionary<PdmExportFormat, IPdmExportTemplate>();
        /// <summary>
        /// 当前导出使用的显式模型工作区。
        /// </summary>
        private PdmWorkspace _workspace;
        /// <summary>
        /// 当前导出模型的工作区键。
        /// </summary>
        private string _modelKey;
        /// <summary>
        /// 当前导出的图形渲染选项。
        /// </summary>
        private PdmDiagramRenderOptions _renderOptions;

        /// <summary>
        /// 初始化一个 <see cref="PdmExporter"/> 类型的实例。
        /// </summary>
        public PdmExporter() : this(PdmExportLanguage.English, null, null) { }

        /// <summary>
        /// 初始化一个 <see cref="PdmExporter"/> 类型的实例。
        /// </summary>
        /// <param name="language">导出语言。</param>
        public PdmExporter(PdmExportLanguage language) : this(language, null, null) { }

        /// <summary>
        /// 初始化一个 <see cref="PdmExporter"/> 类型的实例。
        /// </summary>
        /// <param name="language">导出语言。</param>
        /// <param name="labelProvider">可选的标签提供器。</param>
        public PdmExporter(PdmExportLanguage language, IPdmExportLabelProvider labelProvider) : this(language, labelProvider, null) { }

        /// <summary>
        /// 初始化一个 <see cref="PdmExporter"/> 类型的实例。
        /// </summary>
        /// <param name="language">导出语言。</param>
        /// <param name="labelProvider">可选的标签提供器。</param>
        /// <param name="templates">可选的 Markdown 或 HTML 模板。</param>
        /// <exception cref="ArgumentException"><paramref name="templates"/> 包含空项、重复格式或非 Markdown/HTML 格式。</exception>
        public PdmExporter(PdmExportLanguage language, IPdmExportLabelProvider labelProvider,
            IEnumerable<IPdmExportTemplate> templates)
        {
            _language = language;
            _labelProvider = labelProvider;
            if (templates == null) return;
            foreach (var template in templates)
            {
                if (template == null) throw new ArgumentException("Export templates cannot contain null values.", nameof(templates));
                if (template.Format != PdmExportFormat.Markdown && template.Format != PdmExportFormat.Html)
                    throw new ArgumentException("Custom templates are supported for Markdown and HTML only.", nameof(templates));
                if (_templates.ContainsKey(template.Format))
                    throw new ArgumentException("Only one template can be registered for each export format.", nameof(templates));
                _templates.Add(template.Format, template);
            }
        }

        /// <inheritdoc />
        public void Write(PdmInfo model, PdmExportFormat format, TextWriter writer)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            IPdmExportTemplate template;
            if (_templates.TryGetValue(format, out template))
            {
                template.Write(model, this, writer);
                return;
            }
            switch (format)
            {
                case PdmExportFormat.Json:
                    var json = JObject.FromObject(model);
                    json.AddFirst(new JProperty("SchemaVersion", Reader.PdmJsonMigrator.CurrentVersion));
                    if (_workspace != null) json["Diagnostics"] = JArray.FromObject(EffectiveDiagnostics(model));
                    writer.Write(json.ToString(Formatting.Indented));
                    return;
                case PdmExportFormat.Markdown:
                    WriteMarkdown(model, writer);
                    return;
                case PdmExportFormat.Html:
                    WriteHtml(model, writer);
                    return;
                case PdmExportFormat.Svg:
                    var diagrams = model.AllPhysicalDiagrams.ToArray();
                    if (diagrams.Length != 1)
                        throw new InvalidOperationException("SVG export requires exactly one physical diagram. Use WriteDiagram for a selected diagram.");
                    WriteDiagram(model, diagrams[0], writer);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        /// <summary>
        /// 使用指定图形风格导出模型。
        /// </summary>
        public void Write(PdmInfo model, PdmExportFormat format, TextWriter writer, PdmDiagramRenderOptions options)
        {
            _renderOptions = options;
            try { Write(model, format, writer); }
            finally { _renderOptions = null; }
        }

        /// <summary>
        /// 在工作区上下文中导出指定模型。
        /// </summary>
        public void Write(PdmWorkspace workspace, string modelKey, PdmExportFormat format, TextWriter writer)
        {
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (!workspace.TryGetModel(modelKey, out var model))
                throw new ArgumentException("Unknown workspace model key.", nameof(modelKey));
            _workspace = workspace;
            _modelKey = modelKey;
            try { Write(model, format, writer); }
            finally { _workspace = null; _modelKey = null; }
        }

        /// <summary>
        /// 在工作区中使用指定图形风格导出模型。
        /// </summary>
        public void Write(PdmWorkspace workspace, string modelKey, PdmExportFormat format,
            TextWriter writer, PdmDiagramRenderOptions options)
        {
            _renderOptions = options;
            try { Write(workspace, modelKey, format, writer); }
            finally { _renderOptions = null; }
        }

        /// <inheritdoc />
        public void WriteDiagram(PdmInfo model, Bing.Pdm.Models.PhysicalDiagrams.PhysicalDiagramInfo diagram, TextWriter writer)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (diagram == null) throw new ArgumentNullException(nameof(diagram));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            SvgDiagramRenderer.Write(model, diagram, writer, false, _renderOptions, _workspace, _modelKey);
        }

        /// <summary>
        /// 使用指定图形风格导出 SVG。
        /// </summary>
        public void WriteDiagram(PdmInfo model, Bing.Pdm.Models.PhysicalDiagrams.PhysicalDiagramInfo diagram,
            TextWriter writer, PdmDiagramRenderOptions options)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (diagram == null) throw new ArgumentNullException(nameof(diagram));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            SvgDiagramRenderer.Write(model, diagram, writer, false, options, _workspace, _modelKey);
        }

        /// <summary>
        /// 在工作区上下文中导出独立 SVG。
        /// </summary>
        public void WriteDiagram(PdmWorkspace workspace, string modelKey,
            Bing.Pdm.Models.PhysicalDiagrams.PhysicalDiagramInfo diagram, TextWriter writer,
            PdmDiagramRenderOptions options = null)
        {
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (!workspace.TryGetModel(modelKey, out var model))
                throw new ArgumentException("Unknown workspace model key.", nameof(modelKey));
            SvgDiagramRenderer.Write(model, diagram, writer, false, options, workspace, modelKey);
        }

        /// <summary>
        /// 写入 Markdown 数据字典。
        /// </summary>
        /// <param name="model">待导出的 PDM 模型。</param>
        /// <param name="writer">Markdown 输出。</param>
        private void WriteMarkdown(PdmInfo model, TextWriter writer)
        {
            writer.WriteLine("# " + Md(model.Name ?? model.Code ?? L("DataDictionary")));
            writer.WriteLine();
            writer.WriteLine(L("Dbms") + ": " + Md(model.DbmsName ?? model.DbmsCode ?? L("Unknown")));
            if (!string.IsNullOrEmpty(model.Comment)) writer.WriteLine(Md(model.Comment));
            writer.WriteLine();
            foreach (var item in TablesWithPaths(model))
            {
                var table = item.Item1;
                writer.WriteLine("## " + Md(item.Item2 + table.Code) + " " + Md(table.Name));
                writer.WriteLine();
                if (!string.IsNullOrEmpty(table.Description)) writer.WriteLine(Md(table.Description) + "\n");
                if (!string.IsNullOrEmpty(table.Comment)) writer.WriteLine(L("Comment") + ": " + Md(table.Comment) + "\n");
                WriteOwnerSchema(model, table, writer);
                writer.WriteLine("| " + L("Column") + " | " + L("Name") + " | " + L("Type") + " | " + L("Length") + " | " + L("Precision") + " | " + L("Required") + " | " + L("PrimaryKey") + " | " + L("Identity") + " | " + L("Default") + " | " + L("Comment") + " |");
                writer.WriteLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
                foreach (var column in table.Columns)
                    writer.WriteLine("| " + Md(column.Code) + " | " + Md(column.Name) + " | " + Md(column.DataType) + " | " +
                        Md(column.Length) + " | " + Md(column.Precision) + " | " + L(column.Mandatory ? "Yes" : "No") +
                        " | " + L(column.PrimaryKey ? "Yes" : "No") + " | " + L(column.Identity ? "Yes" : "No") + " | " + Md(column.DefaultValue) + " | " +
                        Md(column.Comment ?? column.Description) + " |");
                writer.WriteLine();
                foreach (var key in table.Keys)
                    writer.WriteLine("- " + (key.Id == table.PrimaryKeyId ? L("PrimaryKey") : L("Key")) + " " + Md(key.Code) + ": " + Md(ColumnCodes(table, key.ColumnIds)));
                foreach (var index in table.Indexes)
                    writer.WriteLine("- " + L("Index") + " " + Md(index.Code) + " (" + L(index.Unique ? "Unique" : "NonUnique") + "): " + Md(ColumnCodes(table, index.ColumnIds)));
                if (table.Indexes.Count > 0 || table.Keys.Count > 0) writer.WriteLine();
            }
            writer.WriteLine("## " + L("References"));
            writer.WriteLine();
            writer.WriteLine("| " + L("Reference") + " | " + L("Parent") + " | " + L("Child") + " | " + L("Columns") + " | " + L("Cardinality") + " |");
            writer.WriteLine("| --- | --- | --- | --- | --- |");
            foreach (var reference in model.AllReferences)
                writer.WriteLine("| " + Md(reference.Code) + " | " + Md(AddressTableCode(model, reference.ParentTableId, reference.ParentTableAddress)) +
                    " | " + Md(AddressTableCode(model, reference.ChildTableId, reference.ChildTableAddress)) + " | " + Md(JoinText(model, reference)) +
                    " | " + Md(reference.Cardinality) + " |");
            writer.WriteLine();
            if (model.AllViews.Any())
            {
                writer.WriteLine("## " + L("Views") + "\n");
                foreach (var view in model.AllViews)
                {
                    writer.WriteLine("### " + Md(view.Code) + " " + Md(view.Name) + "\n");
                    if (!string.IsNullOrEmpty(view.Description)) writer.WriteLine(Md(view.Description) + "\n");
                    if (!string.IsNullOrEmpty(view.Comment)) writer.WriteLine(L("Comment") + ": " + Md(view.Comment) + "\n");
                    if (view.Columns.Count > 0)
                    {
                        writer.WriteLine("| " + L("Column") + " | " + L("Name") + " | " + L("Type") + " | " + L("Length") + " | " + L("Precision") + " | " + L("Required") + " | " + L("Default") + " | " + L("Comment") + " |");
                        writer.WriteLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
                        foreach (var column in view.Columns)
                            writer.WriteLine("| " + Md(column.Code) + " | " + Md(column.Name) + " | " + Md(column.DataType) + " | " + Md(column.Length) + " | " + Md(column.Precision) + " | " + L(column.Mandatory ? "Yes" : "No") + " | " + Md(column.DefaultValue) + " | " + Md(column.Comment ?? column.Description) + " |");
                        writer.WriteLine();
                    }
                    if (!string.IsNullOrEmpty(view.ViewSQLQuery))
                    {
                        foreach (var line in view.ViewSQLQuery.Replace("\r", string.Empty).Split('\n'))
                            writer.WriteLine("    " + line);
                        writer.WriteLine();
                    }
                    if (!string.IsNullOrEmpty(view.TaggedSQLQuery))
                    {
                        writer.WriteLine(L("TaggedSql") + ":");
                        foreach (var line in view.TaggedSQLQuery.Replace("\r", string.Empty).Split('\n'))
                            writer.WriteLine("    " + line);
                        writer.WriteLine();
                    }
                }
                writer.WriteLine();
            }
            WriteMarkdownDependencies(model, writer);
            WriteMarkdownDiagnostics(model, writer);
        }

        /// <summary>
        /// 写入 Markdown 诊断信息。
        /// </summary>
        /// <param name="model">包含诊断信息的 PDM 模型。</param>
        /// <param name="writer">Markdown 输出。</param>
        private void WriteMarkdownDiagnostics(PdmInfo model, TextWriter writer)
        {
            var diagnostics = EffectiveDiagnostics(model);
            if (diagnostics.Length == 0) return;
            writer.WriteLine("## " + L("Diagnostics") + "\n");
            foreach (var diagnostic in diagnostics)
                writer.WriteLine("- " + Md(diagnostic.Code) + " (" + Md(diagnostic.SourceId) + "): " + Md(diagnostic.Message));
        }

        /// <summary>
        /// 写入 HTML 数据字典。
        /// </summary>
        /// <param name="model">待导出的 PDM 模型。</param>
        /// <param name="writer">HTML 输出。</param>
        private void WriteHtml(PdmInfo model, TextWriter writer)
        {
            var lang = _language == PdmExportLanguage.Chinese ? "zh-CN" : "en";
            var title = model.Name ?? model.Code ?? "PDM";
            writer.WriteLine("<!doctype html><html lang=\"" + lang + "\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            writer.WriteLine("<title>" + H(title) + " - " + H(L("DataDictionary")) + "</title>");
            writer.WriteLine("<style>td code{white-space:nowrap;overflow-wrap:normal}</style>");
            writer.WriteLine("<style>body{margin:0;font:14px/1.5 system-ui,Arial,sans-serif;color:#242a30;background:#fff}header{background:#263b3f;color:#fff;padding:20px max(20px,calc((100% - 1180px)/2))}header h1{margin:0;font-size:24px}header p{margin:4px 0 0;color:#dbe5e7}main{max-width:1180px;margin:auto;padding:20px}nav{display:flex;gap:18px;flex-wrap:wrap;border-bottom:1px solid #cbd5d8;padding:10px 0}a{color:#126b73}.search{display:flex;align-items:center;gap:8px;padding:12px 0}.search input{width:min(420px,100%);padding:7px 9px;border:1px solid #9eafb3;border-radius:3px}section{margin:28px 0}h2{font-size:20px;border-bottom:1px solid #d9e0e2;padding-bottom:6px}h3{font-size:17px;margin:22px 0 8px}.path{color:#617078;font-size:12px}.table-wrap,.diagram{overflow:auto;border:1px solid #d9e0e2;border-radius:4px}table{border-collapse:collapse;width:100%;min-width:760px}th,td{padding:7px 9px;text-align:left;border-bottom:1px solid #e3e8ea;vertical-align:top}th{background:#f1f5f5;white-space:nowrap}tr:nth-child(even) td{background:#fafbfb}.diagram svg{display:block;width:100%;min-width:680px;max-height:720px;background:#fbfcfc}.diagram-toolbar{display:flex;gap:4px;justify-content:flex-end;padding:5px;border-bottom:1px solid #d9e0e2}.diagram-toolbar button{width:32px;height:30px;border:1px solid #9eafb3;border-radius:3px;background:#fff;color:#24343a;font-size:16px;cursor:pointer}.metadata{color:#46545a}.metadata li{margin:2px 0}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f1f5f5;padding:10px}code{font-family:ui-monospace,Consolas,monospace;overflow-wrap:anywhere}ul{padding-left:20px}article{margin-bottom:24px}[hidden]{display:none!important}@media print{header{background:#fff;color:#222;padding:0}.diagram{break-inside:avoid}nav,.search,.diagram-toolbar{display:none}}</style></head><body>");
            writer.WriteLine("<header><h1>" + H(title) + "</h1><p>" + H(model.DbmsName ?? model.DbmsCode ?? "") + " · " + H(L("DataDictionary")) + "</p></header><main>");
            writer.WriteLine("<nav aria-label=\"" + H(L("Contents")) + "\"><a href=\"#tables\">" + H(L("Tables")) + "</a><a href=\"#references\">" + H(L("References")) + "</a><a href=\"#views\">" + H(L("Views")) + "</a><a href=\"#diagrams\">" + H(L("Diagrams")) + "</a></nav>");
            writer.WriteLine("<div class=\"search\"><label for=\"dictionary-search\">" + H(L("Search")) + "</label><input id=\"dictionary-search\" type=\"search\" placeholder=\"" + H(L("SearchPlaceholder")) + "\"></div>");
            WriteTableOfContents(model, writer);
            writer.WriteLine("<section id=\"tables\"><h2>" + H(L("Tables")) + "</h2>");
            foreach (var item in TablesWithPaths(model))
            {
                var table = item.Item1;
                writer.WriteLine("<article id=\"" + HtmlIds.Create("table", table.Id) + "\" data-searchable><div class=\"path\">" + H(item.Item2.TrimEnd(' ', '/')) + "</div><h3><code>" + H(table.Code) + "</code> " + H(table.Name) + "</h3>");
                if (!string.IsNullOrEmpty(table.Description)) writer.WriteLine("<p>" + H(table.Description) + "</p>");
                if (!string.IsNullOrEmpty(table.Comment)) writer.WriteLine("<p class=\"metadata\"><strong>" + H(L("Comment")) + ":</strong> " + H(table.Comment) + "</p>");
                WriteOwnerSchemaHtml(model, table, writer);
                writer.WriteLine("<div class=\"table-wrap\"><table><thead><tr><th>" + H(L("Column")) + "</th><th>" + H(L("Name")) + "</th><th>" + H(L("Type")) + "</th><th>" + H(L("Length")) + "</th><th>" + H(L("Precision")) + "</th><th>" + H(L("Required")) + "</th><th>" + H(L("PrimaryKey")) + "</th><th>" + H(L("Identity")) + "</th><th>" + H(L("Default")) + "</th><th>" + H(L("Comment")) + "</th></tr></thead><tbody>");
                foreach (var column in table.Columns)
                    writer.WriteLine("<tr><td><code>" + H(column.Code) + "</code></td><td>" + H(column.Name) + "</td><td>" + H(column.DataType) + "</td><td>" + H(column.Length) + "</td><td>" + H(column.Precision) + "</td><td>" + H(L(column.Mandatory ? "Yes" : "No")) + "</td><td>" + H(L(column.PrimaryKey ? "Yes" : "No")) + "</td><td>" + H(L(column.Identity ? "Yes" : "No")) + "</td><td>" + H(column.DefaultValue) + "</td><td>" + H(column.Comment ?? column.Description) + "</td></tr>");
                writer.WriteLine("</tbody></table></div>");
                WriteKeysAndIndexes(table, writer);
                writer.WriteLine("</article>");
            }
            writer.WriteLine("</section><section id=\"references\"><h2>" + H(L("References")) + "</h2><div class=\"table-wrap\"><table><thead><tr><th>" + H(L("Reference")) + "</th><th>" + H(L("Parent")) + "</th><th>" + H(L("Child")) + "</th><th>" + H(L("Columns")) + "</th><th>" + H(L("Cardinality")) + "</th></tr></thead><tbody>");
            foreach (var reference in model.AllReferences)
                writer.WriteLine("<tr id=\"" + HtmlIds.Create("reference", reference.Id) + "\" data-searchable><td>" + H(reference.Code) + "</td><td>" + AddressTableLink(model, reference.ParentTableId, reference.ParentTableAddress) + "</td><td>" + AddressTableLink(model, reference.ChildTableId, reference.ChildTableAddress) + "</td><td>" + H(JoinText(model, reference)) + "</td><td>" + H(reference.Cardinality) + "</td></tr>");
            writer.WriteLine("</tbody></table></div></section><section id=\"views\"><h2>" + H(L("Views")) + "</h2>");
            foreach (var view in model.AllViews)
            {
                writer.WriteLine("<article id=\"" + HtmlIds.Create("view", view.Id) + "\" data-searchable><h3><code>" + H(view.Code) + "</code> " + H(view.Name) + "</h3>");
                if (!string.IsNullOrEmpty(view.Description)) writer.WriteLine("<p>" + H(view.Description) + "</p>");
                if (!string.IsNullOrEmpty(view.Comment)) writer.WriteLine("<p class=\"metadata\"><strong>" + H(L("Comment")) + ":</strong> " + H(view.Comment) + "</p>");
                if (view.Columns.Count > 0)
                {
                    writer.WriteLine("<div class=\"table-wrap\"><table><thead><tr><th>" + H(L("Column")) + "</th><th>" + H(L("Name")) + "</th><th>" + H(L("Type")) + "</th><th>" + H(L("Length")) + "</th><th>" + H(L("Precision")) + "</th><th>" + H(L("Required")) + "</th><th>" + H(L("Default")) + "</th><th>" + H(L("Comment")) + "</th></tr></thead><tbody>");
                    foreach (var column in view.Columns)
                        writer.WriteLine("<tr><td>" + H(column.Code) + "</td><td>" + H(column.Name) + "</td><td>" + H(column.DataType) + "</td><td>" + H(column.Length) + "</td><td>" + H(column.Precision) + "</td><td>" + H(L(column.Mandatory ? "Yes" : "No")) + "</td><td>" + H(column.DefaultValue) + "</td><td>" + H(column.Comment ?? column.Description) + "</td></tr>");
                    writer.WriteLine("</tbody></table></div>");
                }
                if (!string.IsNullOrEmpty(view.ViewSQLQuery)) writer.WriteLine("<h4>SQL</h4><pre><code>" + H(view.ViewSQLQuery) + "</code></pre>");
                if (!string.IsNullOrEmpty(view.TaggedSQLQuery)) writer.WriteLine("<h4>" + H(L("TaggedSql")) + "</h4><pre><code>" + H(view.TaggedSQLQuery) + "</code></pre>");
                writer.WriteLine("</article>");
            }
            writer.WriteLine("</section><section id=\"diagrams\"><h2>" + H(L("Diagrams")) + "</h2>");
            foreach (var diagram in model.AllPhysicalDiagrams)
            {
                writer.WriteLine("<h3 id=\"" + HtmlIds.Create("diagram", diagram.Id) + "\">" + H(diagram.Name ?? diagram.Code) + "</h3><div class=\"diagram-panel\"><div class=\"diagram-toolbar\"><button type=\"button\" data-zoom=\"out\" aria-label=\"" + H(L("ZoomOut")) + "\" title=\"" + H(L("ZoomOut")) + "\">−</button><button type=\"button\" data-zoom=\"reset\" aria-label=\"" + H(L("ResetZoom")) + "\" title=\"" + H(L("ResetZoom")) + "\">↺</button><button type=\"button\" data-zoom=\"in\" aria-label=\"" + H(L("ZoomIn")) + "\" title=\"" + H(L("ZoomIn")) + "\">+</button></div><div class=\"diagram\">");
                SvgDiagramRenderer.Write(model, diagram, writer, true, _renderOptions, _workspace, _modelKey);
                writer.WriteLine("</div></div>");
            }
            writer.WriteLine("</section>");
            WriteHtmlDependencies(model, writer);
            var diagnostics = EffectiveDiagnostics(model);
            if (diagnostics.Length > 0)
            {
                writer.WriteLine("<section><h2>" + H(L("Diagnostics")) + "</h2><ul>");
                foreach (var diagnostic in diagnostics)
                    writer.WriteLine("<li><code>" + H(diagnostic.Code) + "</code> " + H(diagnostic.SourceId) + ": " + H(diagnostic.Message) + "</li>");
                writer.WriteLine("</ul></section>");
            }
            writer.WriteLine("</main><script>(function(){");
            writer.WriteLine("var search=document.getElementById('dictionary-search');");
            writer.WriteLine("function applyFilter(){var q=search.value.trim().toLowerCase();document.querySelectorAll('[data-searchable]').forEach(function(item){item.hidden=q!==''&&item.textContent.toLowerCase().indexOf(q)<0;});}");
            writer.WriteLine("search.addEventListener('input',applyFilter);");
            writer.WriteLine("document.addEventListener('click',function(event){var link=event.target.closest&&event.target.closest('a[href^=\"#\"]');if(!link)return;var target=document.getElementById(link.getAttribute('href').slice(1));if(target&&target.hidden){search.value='';applyFilter();}});");
            writer.WriteLine("window.addEventListener('hashchange',function(){var target=document.getElementById(location.hash.slice(1));if(target&&target.hidden){search.value='';applyFilter();target.scrollIntoView();}});");
            writer.WriteLine("document.querySelectorAll('.diagram-panel').forEach(function(panel){var svg=panel.querySelector('svg');if(!svg)return;var b=svg.viewBox.baseVal;var original={x:b.x,y:b.y,width:b.width,height:b.height};var scale=1;panel.querySelectorAll('[data-zoom]').forEach(function(button){button.addEventListener('click',function(){var action=button.getAttribute('data-zoom');scale=action==='reset'?1:Math.max(0.25,Math.min(4,scale*(action==='in'?1.25:0.8)));var width=original.width/scale;var height=original.height/scale;svg.setAttribute('viewBox',(original.x+(original.width-width)/2)+' '+(original.y+(original.height-height)/2)+' '+width+' '+height);});});});");
            writer.WriteLine("})();</script></body></html>");
        }

        /// <summary>
        /// 写入 HTML 目录导航。
        /// </summary>
        /// <param name="model">待导出的 PDM 模型。</param>
        /// <param name="writer">HTML 输出。</param>
        private void WriteTableOfContents(PdmInfo model, TextWriter writer)
        {
            writer.WriteLine("<details class=\"toc\"><summary>" + H(L("Contents")) + "</summary><ul>");
            foreach (var item in TablesWithPaths(model))
                writer.WriteLine("<li><a href=\"#" + HtmlIds.Create("table", item.Item1.Id) + "\">" + H(item.Item2 + (item.Item1.Code ?? item.Item1.Name)) + "</a></li>");
            foreach (var view in model.AllViews)
                writer.WriteLine("<li><a href=\"#" + HtmlIds.Create("view", view.Id) + "\">" + H((view.Code ?? view.Name) + " (" + L("Views") + ")") + "</a></li>");
            foreach (var diagram in model.AllPhysicalDiagrams)
                writer.WriteLine("<li><a href=\"#" + HtmlIds.Create("diagram", diagram.Id) + "\">" + H((diagram.Name ?? diagram.Code) + " (" + L("Diagrams") + ")") + "</a></li>");
            writer.WriteLine("</ul></details>");
        }

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
            foreach (var table in package.Tables) yield return Tuple.Create(table, path + " / ");
            foreach (var child in package.Packages)
                foreach (var item in TablesWithPaths(child, path + " / " + (child.Name ?? child.Code ?? child.Id))) yield return item;
        }

        /// <summary>
        /// 写入 Markdown 表的所有者和模式信息。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="table">待写入信息的表。</param>
        /// <param name="writer">Markdown 输出。</param>
        private void WriteOwnerSchema(PdmInfo model, TableInfo table, TextWriter writer)
        {
            var owner = FindOwner(model, table);
            var schema = table.SchemaId == null ? null : owner?.Name ?? owner?.Code ?? table.SchemaId;
            if (!string.IsNullOrEmpty(schema)) writer.WriteLine(L("Schema") + ": " + Md(schema) + "\n");
            else if (owner != null) writer.WriteLine(L("Owner") + ": " + Md(owner.Name ?? owner.Code ?? owner.Id) + "\n");
        }

        /// <summary>
        /// 写入 HTML 表的所有者和模式信息。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="table">待写入信息的表。</param>
        /// <param name="writer">HTML 输出。</param>
        private void WriteOwnerSchemaHtml(PdmInfo model, TableInfo table, TextWriter writer)
        {
            var owner = FindOwner(model, table);
            var schema = table.SchemaId == null ? null : owner?.Name ?? owner?.Code ?? table.SchemaId;
            var value = !string.IsNullOrEmpty(schema) ? L("Schema") + ": " + schema
                : owner == null ? null : L("Owner") + ": " + (owner.Name ?? owner.Code ?? owner.Id);
            if (!string.IsNullOrEmpty(value)) writer.WriteLine("<p class=\"metadata\">" + H(value) + "</p>");
        }

        /// <summary>
        /// 查找表所属的所有者。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="table">待查找所有者的表。</param>
        /// <returns>匹配的所有者；所有者标识为空或无法找到时返回 <see langword="null"/>。</returns>
        private PdmOwnerInfo FindOwner(PdmInfo model, TableInfo table)
        {
            PdmOwnerInfo owner;
            return !string.IsNullOrEmpty(table.OwnerId) && model.Lookup != null && model.Lookup.TryGetOwner(table.OwnerId, out owner) ? owner : null;
        }

        /// <summary>
        /// 写入表的键和索引信息。
        /// </summary>
        /// <param name="table">待写入键和索引的表。</param>
        /// <param name="writer">HTML 输出。</param>
        private void WriteKeysAndIndexes(TableInfo table, TextWriter writer)
        {
            if (table.Keys.Count > 0)
            {
                writer.Write("<p><strong>" + H(L("Keys")) + ":</strong> ");
                writer.Write(string.Join("; ", table.Keys.Select(key => H((key.Id == table.PrimaryKeyId ? L("PrimaryKey") + " " : L("Key") + " ") + key.Code + " (" + ColumnCodes(table, key.ColumnIds) + ")"))));
                writer.WriteLine("</p>");
            }
            foreach (var index in table.Indexes)
                writer.WriteLine("<p><strong>" + H(L("Index")) + ":</strong> " + H(index.Code + " (" + L(index.Unique ? "Unique" : "NonUnique") + "): " + ColumnCodes(table, index.ColumnIds)) + "</p>");
        }

        /// <summary>
        /// 按标识获取列代码列表。
        /// </summary>
        /// <param name="table">包含列的表。</param>
        /// <param name="ids">列标识集合。</param>
        /// <returns>按标识顺序连接的列代码；未找到的列使用其标识。</returns>
        private static string ColumnCodes(TableInfo table, IEnumerable<string> ids) =>
            string.Join(", ", ids.Select(id => table.Columns.FirstOrDefault(x => x.Id == id)?.Code ?? id ?? string.Empty));

        /// <summary>
        /// 生成指向表条目的 HTML 链接。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="id">表标识。</param>
        /// <returns>表存在时返回其字典锚点链接，否则返回转义后的标识。</returns>
        private static string TableLink(PdmInfo model, string id)
        {
            TableInfo table;
            return model.Lookup != null && model.Lookup.TryGetTable(id, out table)
                ? "<a href=\"#" + HtmlIds.Create("table", table.Id) + "\">" + H(table.Code ?? table.Name ?? table.Id) + "</a>"
                : H(id);
        }

        /// <summary>
        /// 显示跨模型表名称，且仅链接当前模型的表。
        /// </summary>
        private string AddressTableLink(PdmInfo model, string id, PdmObjectAddress address)
        {
            if (_workspace == null || address == null || address.ModelKey == _modelKey)
                return TableLink(model, id);
            return H(AddressTableCode(model, id, address));
        }

        /// <summary>
        /// 在工作区上下文中取得表名称。
        /// </summary>
        private string AddressTableCode(PdmInfo model, string id, PdmObjectAddress address)
        {
            if (_workspace == null || address == null || address.ModelKey == _modelKey)
                return TableCode(model, id);
            if (_workspace.TryGetModel(address.ModelKey, out var target) &&
                target.Lookup.TryGetTable(address.PdmId, out var table))
                return address.ModelKey + ":" + (table.Code ?? table.Name ?? table.Id);
            return address.ModelKey + ":" + address.PdmId;
        }

        /// <summary>
        /// 写入模型外部依赖和复制来源。
        /// </summary>
        private void WriteMarkdownDependencies(PdmInfo model, TextWriter writer)
        {
            var links = model.AllReferences.SelectMany(x => new[] { x.ParentTableAddress, x.ChildTableAddress })
                .Where(x => x != null && x.ModelKey != _modelKey && _workspace != null).Distinct().ToArray();
            var origins = model.AllReplications.Where(x => x.OriginalAddress != null).ToArray();
            if (links.Length == 0 && origins.Length == 0) return;
            writer.WriteLine("## External dependencies and replication origins");
            writer.WriteLine();
            foreach (var link in links)
                writer.WriteLine("- " + Md(AddressTableCode(model, link.PdmId, link)));
            foreach (var origin in origins)
                writer.WriteLine("- " + Md(origin.Id) + " <= " + Md(origin.OriginalAddress.ModelKey + ":" + origin.OriginalAddress.PdmId));
            writer.WriteLine();
        }

        /// <summary>
        /// 写入 HTML 外部依赖和复制来源。
        /// </summary>
        private void WriteHtmlDependencies(PdmInfo model, TextWriter writer)
        {
            var links = model.AllReferences.SelectMany(x => new[] { x.ParentTableAddress, x.ChildTableAddress })
                .Where(x => x != null && x.ModelKey != _modelKey && _workspace != null).Distinct().ToArray();
            var origins = model.AllReplications.Where(x => x.OriginalAddress != null).ToArray();
            if (links.Length == 0 && origins.Length == 0) return;
            writer.WriteLine("<section><h2>External dependencies and replication origins</h2><ul>");
            foreach (var link in links)
                writer.WriteLine("<li>" + H(AddressTableCode(model, link.PdmId, link)) + "</li>");
            foreach (var origin in origins)
                writer.WriteLine("<li>" + H(origin.Id) + " &larr; " + H(origin.OriginalAddress.ModelKey + ":" + origin.OriginalAddress.PdmId) + "</li>");
            writer.WriteLine("</ul></section>");
        }

        /// <summary>
        /// 获取当前导出上下文中的有效诊断。
        /// </summary>
        private PdmDiagnostic[] EffectiveDiagnostics(PdmInfo model) =>
            (_workspace == null ? model.Diagnostics.AsEnumerable() : _workspace.GetDiagnostics(_modelKey)).ToArray();

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
        /// 格式化引用的列关联。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="reference">待格式化的引用。</param>
        /// <returns>按父列到子列格式连接的关联文本。</returns>
        private string JoinText(PdmInfo model, ReferenceInfo reference) =>
            string.Join(", ", reference.Joins.Select(join =>
                AddressColumnCode(model, join.ParentColumnId, join.ParentColumnAddress) + " -> " +
                AddressColumnCode(model, join.ChildColumnId, join.ChildColumnAddress)));

        /// <summary>
        /// 在工作区上下文中取得列名称。
        /// </summary>
        private string AddressColumnCode(PdmInfo model, string id, PdmObjectAddress address)
        {
            if (_workspace == null || address == null || address.ModelKey == _modelKey)
                return ColumnCode(model, id);
            if (_workspace.TryGetModel(address.ModelKey, out var target) &&
                target.Lookup.TryGetColumn(address.PdmId, out var column))
                return address.ModelKey + ":" + (column.Code ?? column.Name ?? column.Id);
            return address.ModelKey + ":" + address.PdmId;
        }

        /// <summary>
        /// 按标识获取列代码。
        /// </summary>
        /// <param name="model">所属 PDM 模型。</param>
        /// <param name="id">列标识。</param>
        /// <returns>列代码或名称；列未找到时返回标识，标识为空时返回空字符串。</returns>
        private static string ColumnCode(PdmInfo model, string id)
        {
            ColumnInfo column;
            return model.Lookup != null && model.Lookup.TryGetColumn(id, out column) ? column.Code ?? column.Name ?? id : id ?? string.Empty;
        }

        /// <summary>
        /// 解析标签并应用内置语言回退。
        /// </summary>
        /// <param name="key">标签键。</param>
        /// <returns>外部提供器或内置语言对应的标签文本；键为空时可能返回 <see langword="null"/>。</returns>
        private string L(string key)
        {
            var customLabel = _labelProvider?.GetLabel(key);
            if (!string.IsNullOrEmpty(customLabel)) return customLabel;
            if (_language != PdmExportLanguage.Chinese) return EnglishLabel(key);
            switch (key)
            {
                case "DataDictionary": return "数据字典";
                case "Dbms": return "数据库";
                case "Unknown": return "未知";
                case "Comment": return "注释";
                case "Column": return "字段";
                case "Name": return "名称";
                case "Type": return "类型";
                case "Length": return "长度";
                case "Precision": return "精度";
                case "Required": return "必填";
                case "PrimaryKey": return "主键";
                case "Identity": return "自增";
                case "Default": return "默认值";
                case "Key": return "键";
                case "Keys": return "键定义";
                case "Index": return "索引";
                case "Unique": return "唯一";
                case "NonUnique": return "非唯一";
                case "References": return "关系";
                case "Reference": return "关系名称";
                case "Parent": return "父表";
                case "Child": return "子表";
                case "Columns": return "关联字段";
                case "Cardinality": return "基数";
                case "Views": return "视图";
                case "Diagnostics": return "诊断";
                case "Contents": return "目录";
                case "Tables": return "数据表";
                case "Search": return "搜索";
                case "SearchPlaceholder": return "搜索表、视图和关系";
                case "Diagrams": return "物理图";
                case "ZoomOut": return "缩小";
                case "ZoomIn": return "放大";
                case "ResetZoom": return "重置缩放";
                case "TaggedSql": return "标签 SQL";
                case "Schema": return "架构";
                case "Owner": return "所有者";
                case "TableId": return "数据表 ID";
                case "ColumnId": return "字段 ID";
                case "ViewId": return "视图 ID";
                case "Id": return "ID";
                case "Table": return "数据表";
                case "View": return "视图";
                case "Package": return "包";
                case "Description": return "说明";
                case "Kind": return "类型";
                case "ParentKey": return "父键";
                case "Code": return "代码";
                case "SourceId": return "源对象 ID";
                case "Message": return "信息";
                case "SheetColumns": return "字段";
                case "SheetKeysIndexes": return "键与索引";
                case "SheetReferences": return "关系";
                case "SheetViews": return "视图";
                case "SheetViewColumns": return "视图字段";
                case "SheetDiagnostics": return "诊断";
                case "Yes": return "是";
                case "No": return "否";
                default: return key;
            }
        }

        /// <summary>
        /// 获取指定标签文本。
        /// </summary>
        /// <param name="key">标签键。</param>
        /// <returns>按当前导出语言解析的标签文本；键为空时可能返回 <see langword="null"/>。</returns>
        internal string Label(string key) => L(key);

        /// <inheritdoc />
        public string GetLabel(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            return L(key);
        }

        /// <summary>
        /// 获取内置英文标签。
        /// </summary>
        /// <param name="key">标签键。</param>
        /// <returns>匹配的英文标签；未定义时返回原键，键为 <see langword="null"/> 时返回 <see langword="null"/>。</returns>
        private static string EnglishLabel(string key)
        {
            switch (key)
            {
                case "Dbms": return "DBMS";
                case "Keys": return "Keys";
                case "DataDictionary": return "Data dictionary";
                case "Unknown": return "unknown";
                case "PrimaryKey": return "Primary key";
                case "NonUnique": return "non-unique";
                case "Contents": return "Contents";
                case "SearchPlaceholder": return "Search tables, views, and references";
                case "TaggedSql": return "Tagged SQL";
                case "ZoomOut": return "Zoom out";
                case "ZoomIn": return "Zoom in";
                case "ResetZoom": return "Reset zoom";
                case "Yes": return "Yes";
                case "No": return "No";
                case "Owner": return "Owner";
                case "Schema": return "Schema";
                case "TableId": return "Table ID";
                case "ColumnId": return "Column ID";
                case "ViewId": return "View ID";
                case "Id": return "ID";
                case "Table": return "Table";
                case "View": return "View";
                case "Package": return "Package";
                case "Description": return "Description";
                case "Kind": return "Kind";
                case "ParentKey": return "Parent key";
                case "Code": return "Code";
                case "SourceId": return "Source ID";
                case "Message": return "Message";
                case "SheetColumns": return "Columns";
                case "SheetKeysIndexes": return "Keys and Indexes";
                case "SheetReferences": return "References";
                case "SheetViews": return "Views";
                case "SheetViewColumns": return "View Columns";
                case "SheetDiagnostics": return "Diagnostics";
                default: return key;
            }
        }

        /// <summary>
        /// 转义 Markdown 文本。
        /// </summary>
        /// <param name="value">待转义文本。</param>
        /// <returns>替换特殊字符并转义 Markdown 标点后的文本；输入为 <see langword="null"/> 时返回空字符串。</returns>
        private static string Md(string value)
        {
            var text = (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            var builder = new System.Text.StringBuilder(text.Length + 8);
            foreach (var character in text)
            {
                if (character == '\r' || character == '\n') builder.Append(' ');
                else
                {
                    if ("\\|`*_{}[]()#+-.!".IndexOf(character) >= 0) builder.Append('\\');
                    builder.Append(character);
                }
            }
            return builder.ToString();
        }
        /// <summary>
        /// 转义 HTML 文本。
        /// </summary>
        /// <param name="value">待转义文本。</param>
        /// <returns>HTML 转义后的文本；输入为 <see langword="null"/> 时返回空字符串。</returns>
        private static string H(string value) => WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
