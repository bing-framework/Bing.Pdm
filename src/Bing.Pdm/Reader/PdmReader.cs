using Bing.Pdm.Abstractions.Loaders;
using Bing.Pdm.Core;
using Bing.Pdm.Core.Loaders;
using Bing.Pdm.Models;

namespace Bing.Pdm.Reader
{
    /// <summary>
    /// PDM 读取器
    /// </summary>
    public class PdmReader : IPdmReader
    {
        /// <summary>
        /// 加载器
        /// </summary>
        private readonly IPdmLoader _loader;

        /// <summary>
        /// 初始化一个<see cref="PdmReader"/>类型的实例
        /// </summary>
        public PdmReader()
        {
            _loader = new PdmLoader(new LoaderContext());
        }

        /// <summary>
        /// 读取指定PDM文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public PdmInfo ReadFromFile(string filePath) => _loader.GetPdm(filePath);
    }
}
