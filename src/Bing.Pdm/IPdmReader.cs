﻿using Bing.Pdm.Models;

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
        /// <param name="filePath">文件路径</param>
        PdmInfo ReadFromFile(string filePath);
    }
}
