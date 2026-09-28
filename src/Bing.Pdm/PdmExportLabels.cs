using System;
using System.Collections.Generic;

namespace Bing.Pdm
{
    /// <summary>
    /// 提供 PDM 数据字典导出所需的标签。
    /// </summary>
    public interface IPdmExportLabelProvider
    {
        /// <summary>
        /// 按键获取标签文本。
        /// </summary>
        /// <param name="key">标签键。</param>
        /// <returns>标签文本；未找到时返回 <see langword="null"/>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> 为 <see langword="null"/>。</exception>
        string GetLabel(string key);
    }

    /// <summary>
    /// 从调用方提供的字典中获取导出标签。
    /// </summary>
    public sealed class DictionaryPdmExportLabelProvider : IPdmExportLabelProvider
    {
        /// <summary>
        /// 按忽略大小写的键查找调用方提供的自定义标签。
        /// </summary>
        private readonly Dictionary<string, string> _labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 初始化一个 <see cref="DictionaryPdmExportLabelProvider"/> 类型的实例。
        /// </summary>
        /// <param name="labels">标签键值对。</param>
        /// <exception cref="ArgumentNullException"><paramref name="labels"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException">存在空标签键或忽略大小写后重复的标签键。</exception>
        public DictionaryPdmExportLabelProvider(IDictionary<string, string> labels)
        {
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            foreach (var label in labels)
            {
                if (string.IsNullOrWhiteSpace(label.Key))
                    throw new ArgumentException("Label keys cannot be empty.", nameof(labels));
                _labels.Add(label.Key, label.Value);
            }
        }

        /// <inheritdoc />
        public string GetLabel(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            return _labels.TryGetValue(key, out var value) ? value : null;
        }
    }
}
