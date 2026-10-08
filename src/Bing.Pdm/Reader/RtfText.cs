using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Bing.Pdm.Models.PhysicalDiagrams;

namespace Bing.Pdm.Reader
{
    /// <summary>
    /// 解码 PowerDesigner RTF 文本及分段样式。
    /// </summary>
    internal static class RtfText
    {
        /// <summary>
        /// 用于读取 RTF 声明的 ANSI 代码页。
        /// </summary>
        private static readonly Regex CodePage = new Regex(@"\\ansicpg(\d+)", RegexOptions.Compiled);
        /// <summary>
        /// 解析时需要忽略内容的 RTF 控制组名称。
        /// </summary>
        private static readonly HashSet<string> HiddenGroups = new HashSet<string>(StringComparer.Ordinal)
        {
            "fonttbl", "colortbl", "stylesheet", "info", "pict", "object", "header", "footer"
        };

        /// <summary>
        /// 解码 RTF 文本并返回纯文本。
        /// </summary>
        /// <param name="value">RTF 文本。</param>
        /// <returns>去除格式后的文本；输入为空或不是 RTF 时返回原值。</returns>
        public static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith(@"{\rtf", StringComparison.Ordinal)) return value;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            int page;
            var match = CodePage.Match(value);
            var encoding = match.Success && int.TryParse(match.Groups[1].Value, out page)
                ? Encoding.GetEncoding(page) : Encoding.GetEncoding(1252);
            var result = new StringBuilder();
            var bytes = new List<byte>();
            var stack = new Stack<State>();
            var state = new State();
            Action flush = () =>
            {
                if (bytes.Count == 0) return;
                if (!state.Hidden) result.Append(encoding.GetString(bytes.ToArray()));
                bytes.Clear();
            };
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (ch == '{')
                {
                    flush();
                    stack.Push(state);
                    state = state.Copy();
                    continue;
                }
                if (ch == '}')
                {
                    flush();
                    if (stack.Count > 0) state = stack.Pop();
                    continue;
                }
                if (ch != '\\')
                {
                    flush();
                    if (!state.Hidden && ch != '\r' && ch != '\n' && state.Skip == 0) result.Append(ch);
                    else if (state.Skip > 0) state.Skip--;
                    continue;
                }
                if (++i >= value.Length) break;
                ch = value[i];
                if (ch == '\'' && i + 2 < value.Length)
                {
                    byte b;
                    if (byte.TryParse(value.Substring(i + 1, 2), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out b) && !state.Hidden)
                    {
                        if (state.Skip > 0) state.Skip--;
                        else bytes.Add(b);
                    }
                    i += 2;
                    continue;
                }
                flush();
                if (ch == '*' && state.AtStart) { state.Hidden = true; state.AtStart = false; continue; }
                if (ch == '\\' || ch == '{' || ch == '}')
                {
                    if (!state.Hidden && state.Skip == 0) result.Append(ch);
                    else if (state.Skip > 0) state.Skip--;
                    continue;
                }
                if (!char.IsLetter(ch))
                {
                    if (!state.Hidden && ch == '~') result.Append(' ');
                    continue;
                }
                var start = i;
                while (i + 1 < value.Length && char.IsLetter(value[i + 1])) i++;
                var word = value.Substring(start, i - start + 1);
                var numberStart = i + 1;
                if (numberStart < value.Length && value[numberStart] == '-') numberStart++;
                var end = numberStart;
                while (end < value.Length && char.IsDigit(value[end])) end++;
                var parameter = end > numberStart ? value.Substring(i + 1, end - i - 1) : null;
                i = end - 1;
                if (end < value.Length && value[end] == ' ') i++;
                if (state.AtStart && HiddenGroups.Contains(word)) state.Hidden = true;
                state.AtStart = false;
                if (word == "uc" && int.TryParse(parameter, out var count)) state.UnicodeSkip = count;
                if (state.Hidden) continue;
                if (word == "par" || word == "line") result.Append('\n');
                else if (word == "tab") result.Append('\t');
                else if (word == "u" && short.TryParse(parameter, out var unicode))
                {
                    result.Append((char)unicode);
                    state.Skip = state.UnicodeSkip;
                }
            }
            flush();
            return result.ToString().Trim();
        }

        /// <summary>
        /// 将 RTF 文本解码为连续样式片段。
        /// </summary>
        public static List<PdmRichTextSegmentInfo> DecodeSegments(string value)
        {
            var result = new List<PdmRichTextSegmentInfo>();
            var plain = Decode(value);
            if (string.IsNullOrEmpty(plain)) return result;
            if (string.IsNullOrEmpty(value) || !value.StartsWith(@"{\rtf", StringComparison.Ordinal))
            {
                result.Add(new PdmRichTextSegmentInfo { Text = plain });
                return result;
            }
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var match = CodePage.Match(value);
            var encoding = match.Success && int.TryParse(match.Groups[1].Value, out var page)
                ? Encoding.GetEncoding(page) : Encoding.GetEncoding(1252);
            var fonts = ReadFonts(value, encoding.CodePage);
            var colors = ReadColors(value);
            var defaultFont = Regex.Match(value, @"\\deff(\d+)");
            var state = new State();
            if (defaultFont.Success && int.TryParse(defaultFont.Groups[1].Value, out var defaultIndex))
                fonts.TryGetValue(defaultIndex, out state.FontFamily);
            var stack = new Stack<State>();
            var bytes = new List<byte>();
            var run = new StringBuilder();
            Action flushRun = () =>
            {
                if (run.Length == 0) return;
                result.Add(new PdmRichTextSegmentInfo { Text = run.ToString(), Bold = state.Bold,
                    Italic = state.Italic, FontSize = state.FontSize, Underline = state.Underline,
                    FontFamily = state.FontFamily, ForegroundColor = state.ForegroundColor,
                    ParagraphAlignment = state.ParagraphAlignment });
                run.Clear();
            };
            Action flushBytes = () =>
            {
                if (bytes.Count == 0) return;
                if (!state.Hidden) run.Append(encoding.GetString(bytes.ToArray()));
                bytes.Clear();
            };
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (ch == '{')
                {
                    flushBytes(); stack.Push(state); state = state.Copy(); continue;
                }
                if (ch == '}')
                {
                    flushBytes();
                    if (stack.Count > 0)
                    {
                        var previous = stack.Pop();
                        if (previous.Bold != state.Bold || previous.Italic != state.Italic ||
                            previous.FontSize != state.FontSize || previous.Underline != state.Underline ||
                            previous.FontFamily != state.FontFamily || previous.ForegroundColor != state.ForegroundColor ||
                            previous.ParagraphAlignment != state.ParagraphAlignment) flushRun();
                        state = previous;
                    }
                    continue;
                }
                if (ch != '\\')
                {
                    flushBytes();
                    if (!state.Hidden && ch != '\r' && ch != '\n' && state.Skip == 0) run.Append(ch);
                    else if (state.Skip > 0) state.Skip--;
                    continue;
                }
                if (++i >= value.Length) break;
                ch = value[i];
                if (ch == '\'' && i + 2 < value.Length)
                {
                    if (byte.TryParse(value.Substring(i + 1, 2),
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out var b) && !state.Hidden)
                    {
                        if (state.Skip > 0) state.Skip--;
                        else bytes.Add(b);
                    }
                    i += 2; continue;
                }
                flushBytes();
                if (ch == '*' && state.AtStart) { state.Hidden = true; state.AtStart = false; continue; }
                if (ch == '\\' || ch == '{' || ch == '}')
                {
                    if (!state.Hidden && state.Skip == 0) run.Append(ch);
                    else if (state.Skip > 0) state.Skip--;
                    continue;
                }
                if (!char.IsLetter(ch))
                {
                    if (!state.Hidden && ch == '~') run.Append(' ');
                    continue;
                }
                var start = i;
                while (i + 1 < value.Length && char.IsLetter(value[i + 1])) i++;
                var word = value.Substring(start, i - start + 1);
                var numberStart = i + 1;
                if (numberStart < value.Length && value[numberStart] == '-') numberStart++;
                var end = numberStart;
                while (end < value.Length && char.IsDigit(value[end])) end++;
                var parameter = end > numberStart ? value.Substring(i + 1, end - i - 1) : null;
                i = end - 1;
                if (end < value.Length && value[end] == ' ') i++;
                if (state.AtStart && HiddenGroups.Contains(word)) state.Hidden = true;
                state.AtStart = false;
                if (word == "uc" && int.TryParse(parameter, out var count)) state.UnicodeSkip = count;
                if (state.Hidden) continue;
                if (word == "par" || word == "line") run.Append('\n');
                else if (word == "tab") run.Append('\t');
                else if (word == "u" && short.TryParse(parameter, out var unicode))
                {
                    run.Append((char)unicode); state.Skip = state.UnicodeSkip;
                }
                else if (word == "b" || word == "i" || word == "fs" || word == "plain" ||
                    word == "f" || word == "cf" || word == "ul" || word == "ulnone" ||
                    word == "ql" || word == "qc" || word == "qr" || word == "qj" || word == "pard")
                {
                    flushRun();
                    if (word == "ql" || word == "pard") state.ParagraphAlignment = "Left";
                    else if (word == "qc") state.ParagraphAlignment = "Center";
                    else if (word == "qr") state.ParagraphAlignment = "Right";
                    else if (word == "qj") state.ParagraphAlignment = "Justify";
                    else if (word == "b") state.Bold = parameter != "0";
                    else if (word == "i") state.Italic = parameter != "0";
                    else if (word == "fs" && int.TryParse(parameter, out var halfPoints))
                        state.FontSize = halfPoints / 2;
                    else if (word == "ul") state.Underline = parameter != "0";
                    else if (word == "ulnone") state.Underline = false;
                    else if (word == "f" && int.TryParse(parameter, out var fontIndex))
                        fonts.TryGetValue(fontIndex, out state.FontFamily);
                    else if (word == "cf" && int.TryParse(parameter, out var colorIndex))
                        state.ForegroundColor = colorIndex >= 0 && colorIndex < colors.Count ? colors[colorIndex] : null;
                    else if (word == "plain")
                    {
                        state.Bold = false; state.Italic = false; state.FontSize = null;
                        state.Underline = false; state.ForegroundColor = null; state.FontFamily = null;
                        if (defaultFont.Success && int.TryParse(defaultFont.Groups[1].Value, out var index))
                            fonts.TryGetValue(index, out state.FontFamily);
                    }
                }
            }
            flushBytes(); flushRun();
            if (result.Count > 0) result[0].Text = result[0].Text.TrimStart();
            if (result.Count > 0) result[result.Count - 1].Text = result[result.Count - 1].Text.TrimEnd();
            result.RemoveAll(x => x.Text.Length == 0);
            if (string.Concat(result.ConvertAll(x => x.Text)) == plain) return result;
            return new List<PdmRichTextSegmentInfo> { new PdmRichTextSegmentInfo { Text = plain } };
        }

        /// <summary>
        /// 读取 RTF 字体表。
        /// </summary>
        private static Dictionary<int, string> ReadFonts(string value, int codePage)
        {
            var result = new Dictionary<int, string>();
            foreach (Match font in Regex.Matches(TableContent(value, "fonttbl"), @"\\f(\d+)([^;]*);"))
            {
                if (!int.TryParse(font.Groups[1].Value, out var index)) continue;
                var name = Decode(@"{\rtf1\ansicpg" + codePage + " " + font.Groups[2].Value + "}");
                if (!string.IsNullOrWhiteSpace(name)) result[index] = name;
            }
            return result;
        }

        /// <summary>
        /// 读取 RTF 颜色表。
        /// </summary>
        private static List<string> ReadColors(string value)
        {
            var result = new List<string>();
            var table = TableContent(value, "colortbl");
            foreach (var entry in table.Split(';'))
            {
                var red = Regex.Match(entry, @"\\red(\d+)");
                var green = Regex.Match(entry, @"\\green(\d+)");
                var blue = Regex.Match(entry, @"\\blue(\d+)");
                if (red.Success && green.Success && blue.Success &&
                    byte.TryParse(red.Groups[1].Value, out var r) &&
                    byte.TryParse(green.Groups[1].Value, out var g) &&
                    byte.TryParse(blue.Groups[1].Value, out var b))
                    result.Add("#" + r.ToString("x2") + g.ToString("x2") + b.ToString("x2"));
                else result.Add(null);
            }
            return result;
        }

        /// <summary>
        /// 提取 RTF 控制表的组内容。
        /// </summary>
        private static string TableContent(string value, string name)
        {
            var start = value.IndexOf(@"{\" + name, StringComparison.Ordinal);
            if (start < 0) return string.Empty;
            var depth = 0;
            for (var i = start; i < value.Length; i++)
            {
                if (value[i] == '\\') { i++; continue; }
                if (value[i] == '{') depth++;
                else if (value[i] == '}' && --depth == 0)
                    return value.Substring(start + name.Length + 2, i - start - name.Length - 2);
            }
            return string.Empty;
        }

        /// <summary>
        /// 保存 RTF 文本解析状态。
        /// </summary>
        private sealed class State
        {
            /// <summary>
            /// 当前 RTF 组是否属于隐藏内容。
            /// </summary>
            public bool Hidden;
            /// <summary>
            /// 当前 RTF 组是否尚未读取控制字或文本。
            /// </summary>
            public bool AtStart = true;
            /// <summary>
            /// 读取 Unicode 控制字后需要跳过的回退字符数，默认为一个。
            /// </summary>
            public int UnicodeSkip = 1;
            /// <summary>
            /// 当前仍需跳过的回退字符数。
            /// </summary>
            public int Skip;
            /// <summary>
            /// 当前组是否为粗体。
            /// </summary>
            public bool Bold;
            /// <summary>
            /// 当前组是否为斜体。
            /// </summary>
            public bool Italic;
            /// <summary>
            /// 当前组是否显示下划线。
            /// </summary>
            public bool Underline;
            /// <summary>
            /// 当前组字体。
            /// </summary>
            public string FontFamily;
            /// <summary>
            /// 当前组前景色。
            /// </summary>
            public string ForegroundColor;
            /// <summary>
            /// 当前组字号。
            /// </summary>
            public int? FontSize;
            /// <summary>
            /// 当前段落的对齐方式。
            /// </summary>
            public string ParagraphAlignment;
            /// <summary>
            /// 复制解析状态以初始化嵌套 RTF 组。
            /// </summary>
            /// <returns>复制后的解析状态。</returns>
            public State Copy() => new State { Hidden = Hidden, UnicodeSkip = UnicodeSkip, Skip = Skip,
                Bold = Bold, Italic = Italic, FontSize = FontSize, Underline = Underline,
                FontFamily = FontFamily, ForegroundColor = ForegroundColor, ParagraphAlignment = ParagraphAlignment };
        }
    }
}
