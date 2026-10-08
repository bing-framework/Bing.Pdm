using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bing.Pdm.Models.References;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// 在一个显式清单中命名多个 PDM 模型。
    /// </summary>
    public sealed class PdmWorkspace
    {
        /// <summary>
        /// 按清单键保存模型。
        /// </summary>
        private readonly Dictionary<string, PdmInfo> _models = new Dictionary<string, PdmInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 按清单键保存模型文件路径。
        /// </summary>
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>
        /// 保存本轮解析生成的诊断。
        /// </summary>
        private readonly Dictionary<string, List<PdmDiagnostic>> _diagnostics =
            new Dictionary<string, List<PdmDiagnostic>>(StringComparer.Ordinal);

        /// <summary>
        /// 获取工作区中的命名模型。
        /// </summary>
        public IReadOnlyDictionary<string, PdmInfo> Models => _models;

        /// <summary>
        /// 添加具有唯一键和模型身份的模型。
        /// </summary>
        /// <param name="key">工作区清单中的唯一模型键。</param>
        /// <param name="model">待添加的 PDM 模型。</param>
        /// <param name="path">可选的模型文件路径。</param>
        /// <exception cref="ArgumentException">模型键或模型身份为空，或工作区中已存在相同键或身份。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> 为 <see langword="null"/>。</exception>
        public void Add(string key, PdmInfo model, string path = null)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A model key is required.", nameof(key));
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(model.ObjectId))
                throw new ArgumentException("A workspace model must have an ObjectID.", nameof(model));
            if (_models.ContainsKey(key)) throw new ArgumentException("Duplicate workspace model key: " + key + ".", nameof(key));
            if (_models.Values.Any(x => PdmIdentity.Equals(x.ObjectId, model.ObjectId)))
                throw new ArgumentException("Duplicate workspace model ObjectID: " + model.ObjectId + ".", nameof(model));
            _models.Add(key, model);
            _diagnostics.Add(key, new List<PdmDiagnostic>());
            if (path != null) _paths.Add(key, Path.GetFullPath(path));
        }

        /// <summary>
        /// 按清单键查找模型。
        /// </summary>
        /// <param name="key">工作区清单中的模型键。</param>
        /// <param name="model">找到的模型。</param>
        /// <returns>找到模型时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        public bool TryGetModel(string key, out PdmInfo model) =>
            _models.TryGetValue(key ?? string.Empty, out model);

        /// <summary>
        /// 按清单中的绝对路径查找唯一模型键。
        /// </summary>
        /// <param name="path">模型文件路径。</param>
        /// <returns>匹配的模型键；没有唯一匹配时返回 <see langword="null"/>。</returns>
        public string FindModelKeyByPath(string path)
        {
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var full = Path.GetFullPath(path);
            return _paths.Where(x => string.Equals(x.Value, full, comparison))
                .Select(x => x.Key).SingleOrDefault();
        }

        /// <summary>
        /// 获取模型在工作区解析后的有效诊断。
        /// </summary>
        /// <param name="key">工作区清单中的模型键。</param>
        /// <returns>指定模型尚未解决的诊断。</returns>
        public IEnumerable<PdmDiagnostic> GetDiagnostics(string key)
        {
            if (!_models.TryGetValue(key, out var model))
                throw new ArgumentException("Unknown workspace model key: " + key + ".", nameof(key));
            foreach (var diagnostic in model.Diagnostics)
                if (!IsResolved(model, diagnostic)) yield return diagnostic;
            foreach (var diagnostic in _diagnostics[key]) yield return diagnostic;
        }

        /// <summary>
        /// 清空上次跨模型解析的结果。
        /// </summary>
        internal void ResetDiagnostics()
        {
            foreach (var list in _diagnostics.Values) list.Clear();
        }

        /// <summary>
        /// 获取本轮解析器生成的诊断。
        /// </summary>
        internal IEnumerable<PdmDiagnostic> GetOwnDiagnostics(string key) => _diagnostics[key];

        /// <summary>
        /// 记录指定模型的跨模型诊断。
        /// </summary>
        internal void AddDiagnostic(string key, string code, string sourceId, string message) =>
            _diagnostics[key].Add(new PdmDiagnostic { Code = code, SourceId = sourceId, Message = message });

        /// <summary>
        /// 判断单模型诊断是否已被工作区解析解决。
        /// </summary>
        private static bool IsResolved(PdmInfo model, PdmDiagnostic diagnostic)
        {
            if (diagnostic.Code == "UNRESOLVED_SHORTCUT_TARGET" || diagnostic.Code == "AMBIGUOUS_SHORTCUT_TARGET")
                return model.AllShortcuts.Any(x => x.Id == diagnostic.SourceId && x.ResolvedTargetAddress != null);
            if (diagnostic.Code == "UNRESOLVED_REPLICATION_ORIGIN")
                return model.AllReplications.Any(x => x.Id == diagnostic.SourceId && x.OriginalAddress != null)
                    || model.AllSubReplications.Any(x => x.Id == diagnostic.SourceId && x.OriginalAddress != null);
            if (diagnostic.Code != "UNRESOLVED_REF" || diagnostic.Role == null) return false;
            var reference = model.AllReferences.FirstOrDefault(x => x.Id == diagnostic.SourceId);
            if (reference != null)
            {
                if (diagnostic.Role == "parent table") return reference.ParentTableAddress != null;
                if (diagnostic.Role == "child table") return reference.ChildTableAddress != null;
                if (diagnostic.Role == "parent key") return reference.ParentKeyAddress != null;
            }
            var join = model.AllReferences.SelectMany(x => x.Joins)
                .FirstOrDefault(x => x.Id == diagnostic.SourceId);
            if (join != null && diagnostic.Role == "parent column") return join.ParentColumnAddress != null;
            if (join != null && diagnostic.Role == "child column") return join.ChildColumnAddress != null;
            var symbol = model.AllPhysicalDiagrams.SelectMany(x => x.AllSymbols)
                .FirstOrDefault(x => x.Id == diagnostic.SourceId);
            return diagnostic.Role == "diagram object" && symbol?.ObjectAddress != null;
        }
    }
}
