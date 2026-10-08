using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bing.Pdm.Models.PhysicalDiagrams;

namespace Bing.Pdm
{
    /// <summary>
    /// 按字体度量组织富文本行。
    /// </summary>
    internal static class PdmRichTextLayout
    {
        /// <summary>
        /// 富文本行中的一个文字单元。
        /// </summary>
        internal sealed class Glyph
        {
            /// <summary>
            /// 获取或设置文字。
            /// </summary>
            public string Text;
            /// <summary>
            /// 获取或设置原始片段。
            /// </summary>
            public PdmRichTextSegmentInfo Style;
            /// <summary>
            /// 获取或设置字体名称。
            /// </summary>
            public string Family;
            /// <summary>
            /// 获取或设置图形字号。
            /// </summary>
            public int Size;
            /// <summary>
            /// 获取或设置宽度。
            /// </summary>
            public double Width;
        }

        /// <summary>
        /// 一行富文本布局。
        /// </summary>
        internal sealed class Line
        {
            /// <summary>
            /// 获取行内文字。
            /// </summary>
            public List<Glyph> Glyphs { get; } = new List<Glyph>();
            /// <summary>
            /// 获取或设置段落末行标记。
            /// </summary>
            public bool ParagraphEnd;
            /// <summary>
            /// 获取文字宽度。
            /// </summary>
            public double Width => Glyphs.Sum(x => x.Width);
            /// <summary>
            /// 获取行高。
            /// </summary>
            public int Height => Math.Max(1, Glyphs.Count == 0 ? 800 : Glyphs.Max(x => x.Size) * 6 / 5 + 20);
            /// <summary>
            /// 获取对齐方式。
            /// </summary>
            public string Alignment => Glyphs.FirstOrDefault()?.Style.ParagraphAlignment ?? "Left";
        }

        /// <summary>
        /// 将富文本片段换行为布局行。
        /// </summary>
        /// <param name="segments">待布局的富文本片段。</param>
        /// <param name="family">默认字体名称。</param>
        /// <param name="size">默认图形字号。</param>
        /// <param name="width">可用的行宽。</param>
        /// <param name="options">图形渲染选项。</param>
        /// <param name="sourceId">产生诊断时使用的源对象标识。</param>
        /// <returns>按宽度和段落边界拆分后的富文本行。</returns>
        public static List<Line> Layout(IEnumerable<PdmRichTextSegmentInfo> segments, string family, int size,
            int width, PdmDiagramRenderOptions options, string sourceId)
        {
            var lines = new List<Line>();
            var current = new Line();
            foreach (var segment in segments)
            {
                var enumerator = StringInfo.GetTextElementEnumerator((segment.Text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n'));
                while (enumerator.MoveNext())
                {
                    var text = enumerator.GetTextElement();
                    if (text == "\n")
                    {
                        current.ParagraphEnd = true; lines.Add(current); current = new Line(); continue;
                    }
                    if (text == "\t") text = "    ";
                    var glyph = new Glyph { Text = text, Style = segment,
                        Family = segment.FontFamily ?? family, Size = segment.FontSize.HasValue ? PdmDiagramFontMetrics.NativeSize(segment.FontSize.Value * 100) : size };
                    glyph.Width = PdmDiagramFontMetrics.Measure(text, glyph.Size, glyph.Family,
                        segment.Bold, segment.Italic, options, sourceId);
                    if (current.Glyphs.Count > 0 && current.Width + glyph.Width > Math.Max(1, width))
                    {
                        var lastSpace = current.Glyphs.FindLastIndex(x => string.IsNullOrWhiteSpace(x.Text));
                        var next = new Line();
                        if (lastSpace >= 0 && lastSpace < current.Glyphs.Count - 1)
                        {
                            next.Glyphs.AddRange(current.Glyphs.Skip(lastSpace + 1));
                            current.Glyphs.RemoveRange(lastSpace + 1, current.Glyphs.Count - lastSpace - 1);
                        }
                        lines.Add(current); current = next;
                        if (current.Glyphs.Count > 0 && current.Width + glyph.Width > Math.Max(1, width))
                        { lines.Add(current); current = new Line(); }
                    }
                    current.Glyphs.Add(glyph);
                }
            }
            current.ParagraphEnd = true;
            if (current.Glyphs.Count > 0 || lines.Count == 0) lines.Add(current);
            return lines;
        }
    }
}
