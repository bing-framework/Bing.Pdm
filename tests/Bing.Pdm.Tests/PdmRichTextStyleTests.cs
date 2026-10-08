using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Bing.Pdm.Models;
using Bing.Pdm.Reader;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证富文本分段及图元颜色样式。
    /// </summary>
    public sealed class PdmRichTextStyleTests
    {
        /// <summary>
        /// 读取富文本样式样例。
        /// </summary>
        private static PdmInfo ReadModel() => new PdmReader().ReadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "rich-text-styles.pdm"));

        /// <summary>
        /// 验证嵌套 RTF 组的字体、颜色与下划线作用域。
        /// </summary>
        [Fact]
        public void RetainsScopedRunStylesAndCp936Text()
        {
            var model = ReadModel();
            var note = model.AllPhysicalDiagrams.Single().AllSymbols.Single(x => x.Id == "note");
            Assert.Equal("Root Red 你好 Back End Plain", note.Text);
            Assert.Equal(note.Text, string.Concat(note.RichTextSegments.Select(x => x.Text)));
            var red = note.RichTextSegments.First(x => x.Text.Contains("Red"));
            Assert.Equal("Times New Roman", red.FontFamily);
            Assert.Equal("#ff0000", red.ForegroundColor);
            Assert.True(red.Underline);
            Assert.Equal(12, red.FontSize);
            var chinese = note.RichTextSegments.Single(x => x.Text.Contains("你好"));
            Assert.Equal("#0000ff", chinese.ForegroundColor);
            Assert.True(chinese.Bold);
            Assert.True(chinese.Underline);
            Assert.Equal("Times New Roman", chinese.FontFamily);
            var back = note.RichTextSegments.Single(x => x.Text.Contains("Back"));
            Assert.False(back.Bold);
            Assert.Equal("#ff0000", back.ForegroundColor);
            var end = note.RichTextSegments.Last();
            Assert.Equal("Arial", end.FontFamily);
            Assert.False(end.Underline);
            Assert.Null(end.ForegroundColor);
            Assert.Null(end.FontSize);
        }

        /// <summary>
        /// 验证样式在 JSON 往返及 SVG 输出中保持一致。
        /// </summary>
        [Fact]
        public void RendersRunStylesAndIndependentShadowColorsAfterJsonRoundTrip()
        {
            var model = ReadModel();
            using var json = new StringWriter();
            new PdmExporter().Write(model, PdmExportFormat.Json, json);
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(json.ToString()));
            var restored = new PdmJsonReader().Read(input);
            using var output = new StringWriter();
            var options = new PdmDiagramRenderOptions { Style = PdmDiagramStyle.PowerDesigner, FontAvailable = _ => true };
            new PdmExporter().WriteDiagram(restored, restored.AllPhysicalDiagrams.Single(), output, options);
            var svg = XDocument.Parse(output.ToString());
            XNamespace ns = "http://www.w3.org/2000/svg";
            var red = svg.Descendants(ns + "tspan").First(x => x.Value.Contains("Red"));
            Assert.Equal("Times New Roman", (string)red.Attribute("font-family"));
            Assert.Equal("#ff0000", (string)red.Attribute("fill"));
            Assert.Equal("underline", (string)red.Attribute("text-decoration"));
            var chinese = svg.Descendants(ns + "tspan").Single(x => x.Value.Contains("你好"));
            Assert.Equal("#0000ff", (string)chinese.Attribute("fill"));
            Assert.Equal("bold", (string)chinese.Attribute("font-weight"));
            Assert.Equal(new[] { "#ff0000", "#0000ff" }, svg.Descendants(ns + "feDropShadow")
                .Select(x => (string)x.Attribute("flood-color")).ToArray());
            var filters = svg.Descendants(ns + "filter").Select(x => (string)x.Attribute("id")).ToArray();
            Assert.Equal(2, filters.Distinct().Count());
        }

        /// <summary>
        /// 验证富文本内部使用的缺失字体产生诊断。
        /// </summary>
        [Fact]
        public void ChecksFontsDeclaredOnlyInRtfRuns()
        {
            var model = ReadModel();
            var options = new PdmDiagramRenderOptions
            {
                Style = PdmDiagramStyle.PowerDesigner,
                FontAvailable = name => name != "Times New Roman"
            };
            using var output = new StringWriter();
            new PdmExporter().WriteDiagram(model, model.AllPhysicalDiagrams.Single(), output, options);
            Assert.Contains(options.Diagnostics, x => x.Code == "MISSING_DIAGRAM_FONT" && x.SourceId == "note"
                && x.Message.Contains("Times New Roman"));
            Assert.False(options.CanCompareToNative);
        }
    }
}
