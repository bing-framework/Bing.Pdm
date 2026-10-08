using System;
using System.IO;
using System.Linq;
using System.Net;
using Bing.Pdm.Models;
using Newtonsoft.Json;

namespace Bing.Pdm
{
    /// <summary>
    /// 将数据库结构差异写为离线报告。
    /// </summary>
    public sealed class PdmDiffExporter
    {
        /// <summary>
        /// 以指定格式写入差异报告。
        /// </summary>
        /// <param name="result">待写出的差异报告。</param>
        /// <param name="format">报告格式。</param>
        /// <param name="writer">报告输出目标。</param>
        public void Write(PdmDiffResult result, PdmExportFormat format, TextWriter writer)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (format == PdmExportFormat.Json)
            {
                writer.Write(JsonConvert.SerializeObject(result, Formatting.Indented));
                return;
            }
            if (format == PdmExportFormat.Markdown)
            {
                writer.WriteLine("# Database structure diff");
                writer.WriteLine();
                writer.WriteLine("Before DBMS: " + EscapeMarkdown(result.BeforeDbms));
                writer.WriteLine("After DBMS: " + EscapeMarkdown(result.AfterDbms));
                writer.WriteLine("Incomplete: " + result.Incomplete);
                writer.WriteLine();
                writer.WriteLine("| Change | Kind | Before | After | Property | Old value | New value |");
                writer.WriteLine("| --- | --- | --- | --- | --- | --- | --- |");
                foreach (var item in result.Changes)
                    writer.WriteLine("| " + EscapeMarkdown(item.ChangeType) + " | " + EscapeMarkdown(item.Kind) +
                        " | " + EscapeMarkdown(item.BeforePath) + " | " + EscapeMarkdown(item.AfterPath) +
                        " | " + EscapeMarkdown(item.Property) + " | " + EscapeMarkdown(item.BeforeValue) +
                        " | " + EscapeMarkdown(item.AfterValue) + " |");
                if (result.ValidationIssues.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine("## Incomplete comparison reasons");
                    writer.WriteLine();
                    writer.WriteLine("| Side | Code | Kind | Path | Role | Target |");
                    writer.WriteLine("| --- | --- | --- | --- | --- | --- |");
                    foreach (var item in result.ValidationIssues)
                        writer.WriteLine("| " + string.Join(" | ", new[] { item.Side, item.Issue.Code,
                            item.Issue.Kind, item.Issue.Path, item.Issue.Role, item.Issue.TargetId }
                            .Select(EscapeMarkdown)) + " |");
                }
                return;
            }
            if (format != PdmExportFormat.Html)
                throw new ArgumentException("Diff reports support json, md and html only.", nameof(format));
            writer.Write("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Database structure diff</title><style>body{font:14px/1.5 system-ui,Arial,sans-serif;color:#263238;margin:24px auto;max-width:1200px;padding:0 16px}h1{font-size:24px}table{border-collapse:collapse;width:100%}th,td{padding:8px;border-bottom:1px solid #d5dddf;text-align:left;vertical-align:top;overflow-wrap:anywhere}th{background:#edf3f3}tr:nth-child(even){background:#f8faf9}</style></head><body><h1>Database structure diff</h1><p>Before DBMS: ");
            writer.Write(WebUtility.HtmlEncode(result.BeforeDbms));
            writer.Write(" | After DBMS: ");
            writer.Write(WebUtility.HtmlEncode(result.AfterDbms));
            writer.Write(" | Incomplete: ");
            writer.Write(result.Incomplete ? "Yes" : "No");
            writer.Write("</p><table><thead><tr><th>Change</th><th>Kind</th><th>Before</th><th>After</th><th>Property</th><th>Old value</th><th>New value</th></tr></thead><tbody>");
            foreach (var item in result.Changes)
            {
                writer.Write("<tr>");
                foreach (var value in new[] { item.ChangeType, item.Kind, item.BeforePath, item.AfterPath,
                    item.Property, item.BeforeValue, item.AfterValue })
                    writer.Write("<td>" + WebUtility.HtmlEncode(value) + "</td>");
                writer.Write("</tr>");
            }
            writer.Write("</tbody></table>");
            if (result.ValidationIssues.Count > 0)
            {
                writer.Write("<h2>Incomplete comparison reasons</h2><table><thead><tr><th>Side</th><th>Code</th><th>Kind</th><th>Path</th><th>Role</th><th>Target</th></tr></thead><tbody>");
                foreach (var item in result.ValidationIssues)
                {
                    writer.Write("<tr>");
                    foreach (var value in new[] { item.Side, item.Issue.Code, item.Issue.Kind,
                        item.Issue.Path, item.Issue.Role, item.Issue.TargetId })
                        writer.Write("<td>" + WebUtility.HtmlEncode(value) + "</td>");
                    writer.Write("</tr>");
                }
                writer.Write("</tbody></table>");
            }
            writer.Write("</body></html>");
        }

        /// <summary>
        /// 转义 Markdown 表格文本。
        /// </summary>
        private static string EscapeMarkdown(string value) =>
            WebUtility.HtmlEncode(value ?? string.Empty).Replace("\\", "\\\\").Replace("|", "\\|")
                .Replace("\r\n", "<br>").Replace("\n", "<br>").Replace("\r", "<br>");
    }
}
