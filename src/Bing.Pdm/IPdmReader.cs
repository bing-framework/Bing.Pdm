using Bing.Pdm.Models;

namespace Bing.Pdm
{
    /// <summary>
    /// PDM 读取器
    /// </summary>
    public interface IPdmReader
    {
        /// <summary>
        /// 读取指定PDM文件
        /// </summary>
        /// <param name="pdmFile">PDM文件名</param>
        PdmInfo ReadFromFile(string pdmFile);
    }
}
