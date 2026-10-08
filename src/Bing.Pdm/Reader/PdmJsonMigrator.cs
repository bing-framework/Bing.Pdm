using System;
using System.Globalization;
using System.IO;
using System.Text;
using Bing.Pdm.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Bing.Pdm.Reader
{
    /// <summary>
    /// 将本项目的历史 PDM JSON 升级到当前格式。
    /// </summary>
    public sealed class PdmJsonMigrator
    {
        /// <summary>
        /// 获取当前 JSON 格式版本。
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// 保存统一模型的固定序列化契约。
        /// </summary>
        private static readonly IContractResolver Contracts = new DefaultContractResolver();

        /// <summary>
        /// 将 JSON 输入升级并写入输出。
        /// </summary>
        /// <param name="input">历史或当前版本的 JSON 流。</param>
        /// <param name="output">迁移后的文本输出。</param>
        public void Migrate(Stream input, TextWriter output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            var root = LoadCurrent(input);
            using (var writer = new JsonTextWriter(output) { Formatting = Formatting.Indented, CloseOutput = false })
                root.WriteTo(writer);
        }

        /// <summary>
        /// 读取并在内存中迁移模型根节点。
        /// </summary>
        internal static JObject LoadCurrent(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            using (var text = new StreamReader(input, new UTF8Encoding(false, true), true, 1024, true))
            using (var reader = new JsonTextReader(text)
            {
                CloseInput = false,
                MaxDepth = 128,
                DateParseHandling = DateParseHandling.None
            })
            {
                var root = JToken.ReadFrom(reader, new JsonLoadSettings
                { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }) as JObject;
                if (root == null || root["Id"]?.Type != JTokenType.String ||
                    string.IsNullOrWhiteSpace((string)root["Id"]) ||
                    !(root["Tables"] is JArray || root["Packages"] is JArray || root["Views"] is JArray ||
                      root["References"] is JArray || root["PhysicalDiagrams"] is JArray))
                    throw new InvalidDataException("The JSON document is not a PDM model export.");
                if (reader.Read()) throw new JsonReaderException("Unexpected content after the PDM model.");
                var versionToken = root["SchemaVersion"];
                if (versionToken != null && versionToken.Type != JTokenType.Integer)
                    throw new InvalidDataException("PDM JSON SchemaVersion must be an integer.");
                if (versionToken != null && !int.TryParse(versionToken.ToString(),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    throw new InvalidDataException("Unsupported PDM JSON SchemaVersion: " + versionToken + ".");
                var version = versionToken == null ? 0 : int.Parse(versionToken.ToString(), CultureInfo.InvariantCulture);
                if (version < 0 || version > CurrentVersion)
                    throw new InvalidDataException("Unsupported PDM JSON SchemaVersion: " + version + ".");
                ValidateValue(root, typeof(PdmInfo));
                if (version == 0) root["SchemaVersion"] = CurrentVersion;
                return root;
            }
        }

        /// <summary>
        /// 检查固定模型契约中的已知字段。
        /// </summary>
        /// <remarks>未知字段保留原文；此操作不校验对象身份或重新解析引用。</remarks>
        private static void ValidateValue(JToken value, Type type, bool collectionItem = false)
        {
            var nullable = Nullable.GetUnderlyingType(type);
            type = nullable ?? type;
            var contract = Contracts.ResolveContract(type);
            if (value.Type == JTokenType.Null)
            {
                if (!collectionItem && (nullable != null || type == typeof(string) || contract is JsonObjectContract)) return;
                throw InvalidField(value, "must not be null");
            }
            if (contract is JsonObjectContract objectContract)
            {
                if (!(value is JObject node)) throw InvalidField(value, "must be an object");
                foreach (var property in node.Properties())
                {
                    var known = objectContract.Properties.GetClosestMatchProperty(property.Name);
                    if (known != null && !known.Ignored) ValidateValue(property.Value, known.PropertyType);
                }
            }
            else if (contract is JsonArrayContract arrayContract)
            {
                if (!(value is JArray array)) throw InvalidField(value, "must be an array");
                foreach (var item in array) ValidateValue(item, arrayContract.CollectionItemType, true);
            }
            else if (contract is JsonDictionaryContract dictionaryContract)
            {
                if (!(value is JObject dictionary)) throw InvalidField(value, "must be an object");
                foreach (var item in dictionary.Properties())
                    ValidateValue(item.Value, dictionaryContract.DictionaryValueType);
            }
            else if (type == typeof(string))
            {
                if (value.Type != JTokenType.String) throw InvalidField(value, "must be a string");
            }
            else if (type == typeof(bool))
            {
                if (value.Type != JTokenType.Boolean) throw InvalidField(value, "must be a boolean");
            }
            else if (type == typeof(DateTime))
            {
                if (value.Type != JTokenType.String || !DateTime.TryParse((string)value,
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                    throw InvalidField(value, "must be a valid date string");
            }
            else if (type == typeof(int))
            {
                if (value.Type != JTokenType.Integer || !int.TryParse(value.ToString(),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    throw InvalidField(value, "must be a 32-bit integer");
            }
            else
            {
                throw new InvalidDataException("Unsupported PDM JSON field contract: " + type.FullName + ".");
            }
        }

        /// <summary>
        /// 创建包含字段路径的输入错误。
        /// </summary>
        private static InvalidDataException InvalidField(JToken value, string requirement) =>
            new InvalidDataException("PDM JSON field '" + value.Path + "' " + requirement + ".");
    }
}
