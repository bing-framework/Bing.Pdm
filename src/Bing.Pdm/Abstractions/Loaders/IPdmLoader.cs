using Bing.Pdm.Models;

namespace Bing.Pdm.Abstractions.Loaders
{
    /// <summary>
    /// PDM加载器
    /// </summary>
    public interface IPdmLoader
    {
        /// <summary>
        /// 获取PDM信息
        /// </summary>
        /// <param name="filePath">文件路径</param>
        PdmInfo GetPdm(string filePath);
    }
}
