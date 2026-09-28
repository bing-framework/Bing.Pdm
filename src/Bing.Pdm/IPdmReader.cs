using System.IO;
using Bing.Pdm.Models;

namespace Bing.Pdm
{
    /// <summary>
    /// 读取 PowerDesigner PDM 文件。
    /// </summary>
    public interface IPdmReader
    {
        /// <summary>
        /// 从文件读取 PDM 模型。
        /// </summary>
        /// <param name="filePath">PDM 文件路径。</param>
        /// <returns>读取后的 PDM 模型。</returns>
        /// <exception cref="System.ArgumentException"><paramref name="filePath"/> 为空或仅包含空白字符。</exception>
        /// <exception cref="System.IO.IOException">读取文件时发生 I/O 错误。</exception>
        PdmInfo ReadFromFile(string filePath);

        /// <summary>
        /// 从流读取 PDM 模型。
        /// </summary>
        /// <param name="stream">包含 PDM XML 的输入流。</param>
        /// <returns>读取后的 PDM 模型。</returns>
        /// <remarks>
        /// 读取从流的当前位置开始；读取完成后不会关闭输入流。
        /// </remarks>
        /// <exception cref="System.ArgumentNullException"><paramref name="stream"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="System.IO.InvalidDataException">文档中不包含模型节点。</exception>
        /// <exception cref="System.Xml.XmlException">输入不是格式正确的 XML 文档。</exception>
        PdmInfo Read(Stream stream);
    }
}
