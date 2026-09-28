using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Bing.Pdm.Reader
{
    /// <summary>
    /// 将 PowerDesigner RTF 文本转换为纯文本。
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
                        bytes.Add(b);
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
            /// 复制解析状态以初始化嵌套 RTF 组。
            /// </summary>
            /// <returns>复制后的解析状态。</returns>
            public State Copy() => new State { Hidden = Hidden, UnicodeSkip = UnicodeSkip, Skip = Skip };
        }
    }
}
