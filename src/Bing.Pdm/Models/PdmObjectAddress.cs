using System;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 在命名模型工作区中定位 PDM 对象。
    /// </summary>
    public sealed class PdmObjectAddress : IEquatable<PdmObjectAddress>
    {
        /// <summary>
        /// 获取或设置模型清单中的键。
        /// </summary>
        public string ModelKey { get; set; }
        /// <summary>
        /// 获取或设置模型内的 PDM ID。
        /// </summary>
        public string PdmId { get; set; }
        /// <summary>
        /// 获取或设置目标对象的稳定 GUID。
        /// </summary>
        public string ObjectId { get; set; }

        /// <inheritdoc />
        public bool Equals(PdmObjectAddress other) =>
            other != null && string.Equals(ModelKey, other.ModelKey, StringComparison.Ordinal) &&
            string.Equals(PdmId, other.PdmId, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => Equals(obj as PdmObjectAddress);

        /// <inheritdoc />
        public override int GetHashCode() =>
            ((ModelKey ?? string.Empty).GetHashCode() * 397) ^ (PdmId ?? string.Empty).GetHashCode();

        /// <inheritdoc />
        public override string ToString() => ModelKey + ":" + PdmId;
    }
}
