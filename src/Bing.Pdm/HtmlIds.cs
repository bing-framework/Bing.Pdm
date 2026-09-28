using System;
using System.Text;

namespace Bing.Pdm
{
    /// <summary>
    /// 生成 HTML 文档中的稳定锚点标识。
    /// </summary>
    internal static class HtmlIds
    {
        /// <summary>
        /// 生成稳定且适合 HTML 锚点的标识。
        /// </summary>
        /// <param name="prefix">标识前缀。</param>
        /// <param name="id">原始对象标识。</param>
        /// <returns>编码后的 HTML 标识。</returns>
        public static string Create(string prefix, string id)
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(id ?? string.Empty))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return prefix + "-" + encoded;
        }
    }
}
