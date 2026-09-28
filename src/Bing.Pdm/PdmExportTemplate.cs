using System.IO;
using Bing.Pdm.Models;

namespace Bing.Pdm
{
    /// <summary>
    /// 替换一种 Markdown 或 HTML 数据字典渲染方式。
    /// </summary>
    public interface IPdmExportTemplate
    {
        /// <summary>
        /// 获取模板对应的导出格式。
        /// </summary>
        PdmExportFormat Format { get; }

        /// <summary>
        /// 将模型写入文本输出。
        /// </summary>
        /// <param name="model">待导出的 PDM 模型。</param>
        /// <param name="labels">标签提供器。</param>
        /// <param name="writer">文本输出。</param>
        void Write(PdmInfo model, IPdmExportLabelProvider labels, TextWriter writer);
    }
}
