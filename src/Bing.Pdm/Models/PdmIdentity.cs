using System;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 统一对象 GUID 的身份表示。
    /// </summary>
    internal static class PdmIdentity
    {
        /// <summary>
        /// 规范化 GUID 文本。
        /// </summary>
        /// <param name="value">待规范化的 GUID 文本。</param>
        /// <returns>可解析 GUID 的 D 格式文本；无法解析时返回大写原文。</returns>
        public static string Normalize(string value) => Guid.TryParse(value, out var guid)
            ? guid.ToString("D") : value?.ToUpperInvariant();

        /// <summary>
        /// 比较对象 GUID 身份。
        /// </summary>
        /// <param name="left">第一个对象 GUID。</param>
        /// <param name="right">第二个对象 GUID。</param>
        /// <returns>规范化后的两个 GUID 相等时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        public static bool Equals(string left, string right) =>
            string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }
}
