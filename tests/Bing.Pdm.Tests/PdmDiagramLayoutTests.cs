using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Bing.Pdm.Models;
using Bing.Pdm.Reader;
using Bing.Pdm.Tool;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证字体度量、富文本行布局和表内清单。
    /// </summary>
    public sealed class PdmDiagramLayoutTests
    {
        /// <summary>
        /// 读取公开样例。
        /// </summary>
        private static PdmInfo Read(string name) => new PdmReader().ReadFromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".pdm"));

        /// <summary>
        /// 导出原生样式 SVG。
        /// </summary>
        private static XDocument Render(PdmInfo model, PdmDiagramRenderOptions options)
        {
            using var writer = new StringWriter();
            new PdmExporter().WriteDiagram(model, model.AllPhysicalDiagrams.Single(), writer, options);
            return XDocument.Parse(writer.ToString());
        }

        /// <summary>
        /// 验证混合段落的换行、对齐、样式、裁剪及 JSON 往返。
        /// </summary>
        [Fact]
        public void ParagraphsWrapWithoutLosingTextAndKeepAlignmentAfterJsonRoundTrip()
        {
            var model = Read("native-paragraphs");
            using var json = new StringWriter();
            new PdmExporter().Write(model, PdmExportFormat.Json, json);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json.ToString()));
            model = new PdmJsonReader().Read(stream);
            var note = model.AllPhysicalDiagrams.Single().AllSymbols.Single(x => x.Kind == "NoteSymbol");
            Assert.Contains(note.RichTextSegments, x => x.ParagraphAlignment == "Center");
            Assert.Contains(note.RichTextSegments, x => x.ParagraphAlignment == "Right");
            var requests = new List<PdmDiagramTextMeasureInfo>();
            var options = new PdmDiagramRenderOptions
            {
                Style = PdmDiagramStyle.PowerDesigner,
                FontAvailable = _ => true,
                MeasureText = request => { requests.Add(request); return StringInfo.ParseCombiningCharacters(request.Text).Length * 400; }
            };
            var svg = Render(model, options);
            XNamespace ns = "http://www.w3.org/2000/svg";
            var node = svg.Descendants(ns + "g").Single(x => (string)x.Attribute("data-symbol-id") == note.Id);
            var lines = node.Descendants(ns + "text").ToArray();
            Assert.True(lines.Length > 3);
            Assert.Equal(note.Text.Replace("\n", ""), string.Concat(lines.Select(x => x.Value)));
            var clip = node.Descendants(ns + "g").Single(x => x.Attribute("clip-path") != null);
            Assert.StartsWith("url(#pd-text-clip-", (string)clip.Attribute("clip-path"));
            var bounds = ((string)node.Attribute("data-bounds")).Split(' ').Select(int.Parse).ToArray();
            Assert.True((double)lines[0].Elements(ns + "tspan").First().Attribute("x") > bounds[0] + 1000);
            Assert.True((double)lines.Last().Elements(ns + "tspan").First().Attribute("x") > bounds[0] + 1000);
            Assert.Contains(requests, x => x.Bold);
            Assert.Equal("750", (string)lines[0].Elements(ns + "tspan").First().Attribute("font-size"));
            Assert.Contains(requests, x => x.FontSize == 750);
            Assert.Contains(requests, x => x.Text.Contains("你"));
            Assert.DoesNotContain(options.Diagnostics, x => x.Code == "APPROXIMATE_FONT_METRICS");
        }

        /// <summary>
        /// 验证换行不会拆开 Unicode 文字单元。
        /// </summary>
        [Fact]
        public void WrapPreservesEmojiAndCombiningCharacters()
        {
            var model = Read("native-paragraphs");
            var note = model.AllPhysicalDiagrams.Single().AllSymbols.Single(x => x.Kind == "NoteSymbol");
            note.Text = "A😀e\u0301你好B";
            note.RawText = @"{\rtf1}";
            note.RichTextSegments.Clear();
            note.RichTextSegments.Add(new Bing.Pdm.Models.PhysicalDiagrams.PdmRichTextSegmentInfo { Text = note.Text });
            var options = new PdmDiagramRenderOptions
            {
                Style = PdmDiagramStyle.PowerDesigner,
                MeasureText = request => StringInfo.ParseCombiningCharacters(request.Text).Length * 1700
            };
            var svg = Render(model, options);
            XNamespace ns = "http://www.w3.org/2000/svg";
            var node = svg.Descendants(ns + "g").Single(x => (string)x.Attribute("data-symbol-id") == note.Id);
            var spans = node.Descendants(ns + "tspan").Select(x => x.Value).ToArray();
            Assert.Equal(note.Text, string.Concat(spans));
            Assert.Contains(spans, x => x.Contains("😀"));
            Assert.Contains(spans, x => x.Contains("e\u0301"));
            Assert.DoesNotContain(spans, x => x.Contains("�"));
        }

        /// <summary>
        /// 验证未知字体度量会标记估算状态。
        /// </summary>
        [Fact]
        public void InvalidMeasurementReportsApproximation()
        {
            var options = new PdmDiagramRenderOptions
            {
                Style = PdmDiagramStyle.PowerDesigner,
                FontAvailable = _ => true,
                MeasureText = _ => double.NaN
            };
            Render(Read("native-paragraphs"), options);
            Assert.Contains(options.Diagnostics, x => x.Code == "APPROXIMATE_FONT_METRICS");
            Assert.False(options.CanCompareToNative);
        }

        /// <summary>
        /// 验证真实触发器字段、索引和图内清单。
        /// </summary>
        [Fact]
        public void ReadsTriggerTextAndRendersNativePane()
        {
            var xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "native-triggers.pdm"));
            xml = xml.Replace("<a:Event>insert</a:Event>", "<a:Event>insert</a:Event><a:Text>select &lt;value&gt;</a:Text>");
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            var model = new PdmReader().Read(stream);
            var table = model.AllTables.Single(x => x.Triggers.Count > 0);
            var trigger = Assert.Single(table.Triggers);
            Assert.Equal("after", trigger.Timing);
            Assert.Equal("insert", trigger.Event);
            Assert.Equal("select <value>", trigger.Body);
            Assert.Equal(table.Id, trigger.TableId);
            Assert.True(model.Lookup.TryGetTrigger(trigger.Id, out var found));
            Assert.Same(trigger, found);
            var options = new PdmDiagramRenderOptions { Style = PdmDiagramStyle.PowerDesigner };
            var svg = Render(model, options);
            XNamespace ns = "http://www.w3.org/2000/svg";
            var upper = svg.Descendants(ns + "g").Single(x => (string)x.Attribute("data-symbol-id") == "o7");
            Assert.Contains("After insert audit", upper.Value);
            Assert.Equal(2, upper.Descendants(ns + "polyline").Count());
            Assert.DoesNotContain(options.Diagnostics, x => x.Code == "UNSUPPORTED_DIAGRAM_STYLE" && x.Message.Contains("Triggers"));
        }

        /// <summary>
        /// 验证键列与显式自定义过滤器保留模型列顺序。
        /// </summary>
        [Fact]
        public void FiltersColumnsAndPreservesDeclaredOrder()
        {
            var model = Read("native-visual");
            var diagram = model.AllPhysicalDiagrams.Single();
            var table = model.AllTables.Single(x => x.Id == "o9");
            table.Columns.Reverse();
            diagram.DisplayPreferences = "[DisplayPreferences\\Object]\nTable.Columns=Yes\nTable.DisplayName=Yes\nTable.Columns._Filter=所有键列 PDMCOLNKEY";
            var options = new PdmDiagramRenderOptions { Style = PdmDiagramStyle.PowerDesigner };
            var svg = Render(model, options);
            XNamespace ns = "http://www.w3.org/2000/svg";
            var upper = svg.Descendants(ns + "g").Single(x => (string)x.Attribute("data-symbol-id") == "o7");
            Assert.Contains("Order ID", upper.Value);
            Assert.DoesNotContain("Customer Name", upper.Value);
            diagram.DisplayPreferences = "[DisplayPreferences\\Object]\nTable.Columns=Yes\nTable.DisplayName=Yes\nTable.Columns._Filter=custom-public";
            options.ColumnFilter = (_, column, expression) => expression == "custom-public" ? (bool?)true : null;
            svg = Render(model, options);
            upper = svg.Descendants(ns + "g").Single(x => (string)x.Attribute("data-symbol-id") == "o7");
            var text = upper.Descendants(ns + "text").Select(x => x.Value).ToArray();
            Assert.True(Array.IndexOf(text, "Customer Name") < Array.IndexOf(text, "Order ID"));
            Assert.DoesNotContain(options.Diagnostics, x => x.Code == "UNSUPPORTED_DIAGRAM_STYLE");
            options.ColumnFilter = (_, _, _) => null;
            Render(model, options);
            Assert.Contains(options.Diagnostics, x => x.Code == "UNSUPPORTED_DIAGRAM_STYLE");
        }

        /// <summary>
        /// 验证 Windows 字体提供器按实际字形测量并释放资源。
        /// </summary>
        [Fact]
        public void WindowsFontProviderMeasuresDifferentGlyphWidths()
        {
            if (!OperatingSystem.IsWindows()) return;
            var metrics = new WindowsDiagramFontMetrics();
            var request = new PdmDiagramTextMeasureInfo { FontFamily = "Arial", FontSize = 800, Text = "WWW" };
            var wide = metrics.Measure(request);
            request.Text = "iii";
            Assert.True(wide > metrics.Measure(request));
            metrics.Dispose();
            Assert.Throws<ObjectDisposedException>(() => metrics.Measure(request));
        }
    }
}
