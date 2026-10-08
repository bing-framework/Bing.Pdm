using System;
using System.Collections.Generic;
using System.IO;
using Bing.Pdm.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bing.Pdm.Reader
{
    /// <summary>
    /// 从显式清单读取多个本地 PDM 模型。
    /// </summary>
    public sealed class PdmWorkspaceReader
    {
        /// <summary>
        /// 读取清单并解析工作区引用。
        /// </summary>
        /// <param name="path">工作区清单文件路径。</param>
        /// <returns>读取并完成引用解析的工作区。</returns>
        /// <exception cref="ArgumentException">清单路径为空。</exception>
        /// <exception cref="InvalidDataException">清单格式或模型文件格式无效。</exception>
        public PdmWorkspace ReadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A manifest path is required.", nameof(path));
            var manifestPath = Path.GetFullPath(path);
            var root = JObject.Parse(File.ReadAllText(manifestPath));
            if (root["WorkspaceVersion"]?.Type != JTokenType.Integer ||
                root["WorkspaceVersion"].ToString() != "1")
                throw new InvalidDataException("WorkspaceVersion must be 1.");
            if (!(root["Models"] is JArray entries) || entries.Count == 0)
                throw new InvalidDataException("Models must be a nonempty array.");
            var workspace = new PdmWorkspace();
            var directory = Path.GetDirectoryName(manifestPath);
            foreach (var entry in entries)
            {
                if (!(entry is JObject item) || item["Key"]?.Type != JTokenType.String ||
                    item["Path"]?.Type != JTokenType.String)
                    throw new InvalidDataException("Each model requires a Key and Path string.");
                var key = item.Value<string>("Key");
                var relative = item.Value<string>("Path");
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(relative))
                    throw new InvalidDataException("Model Key and Path cannot be empty.");
                var modelPath = Path.GetFullPath(Path.Combine(directory, relative));
                var extension = Path.GetExtension(modelPath);
                PdmInfo model;
                if (extension.Equals(".pdm", StringComparison.OrdinalIgnoreCase))
                    model = new PdmReader().ReadFromFile(modelPath);
                else if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    model = new PdmJsonReader().ReadFromFile(modelPath);
                else throw new InvalidDataException("Workspace model must be .pdm or .json: " + modelPath);
                try { workspace.Add(key, model, modelPath); }
                catch (ArgumentException error) { throw new InvalidDataException("Invalid workspace entry '" + key + "': " + error.Message, error); }
            }
            new PdmWorkspaceResolver().Resolve(workspace);
            return workspace;
        }
    }
}
