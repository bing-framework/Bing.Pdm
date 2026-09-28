using System.IO;
using Bing.Pdm.Models;

namespace Bing.Pdm.Reader
{
    /// <summary>
    /// 读取 PowerDesigner PDM 文件的默认实现。
    /// </summary>
    public sealed class PdmReader : IPdmReader
    {
        /// <inheritdoc />
        public PdmInfo ReadFromFile(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
                return Read(stream);
        }

        /// <inheritdoc />
        public PdmInfo Read(Stream stream) => new PowerDesignerParser().Parse(stream);
    }
}
