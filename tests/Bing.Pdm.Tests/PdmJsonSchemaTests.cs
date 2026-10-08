using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Bing.Pdm.Models;
using Bing.Pdm.Models.Others;
using Bing.Pdm.Models.PhysicalDiagrams;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Reader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证 JSON schema 覆盖当前导出模型的公共字段。
    /// </summary>
    public sealed class PdmJsonSchemaTests
    {
        /// <summary>
        /// 验证主要模型类型的可序列化属性均有 schema 描述。
        /// </summary>
        [Fact]
        public void SchemaCoversSerializedModelProperties()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Schemas", "pdm-v1.schema.json");
            var schema = JObject.Parse(File.ReadAllText(path));
            var contracts = new[]
            {
                Tuple.Create(typeof(PdmInfo), (JToken)schema),
                Tuple.Create(typeof(PackageInfo), schema["definitions"]["package"]),
                Tuple.Create(typeof(TableInfo), schema["definitions"]["table"]),
                Tuple.Create(typeof(ColumnInfo), schema["definitions"]["column"]),
                Tuple.Create(typeof(PdmTriggerInfo), schema["definitions"]["trigger"]),
                Tuple.Create(typeof(ReferenceInfo), schema["definitions"]["reference"]),
                Tuple.Create(typeof(TargetModelInfo), schema["definitions"]["targetModel"]),
                Tuple.Create(typeof(PdmReplicationInfo), schema["definitions"]["replication"]),
                Tuple.Create(typeof(DiagramSymbolInfo), schema["definitions"]["symbol"]),
                Tuple.Create(typeof(PdmRichTextSegmentInfo), schema["definitions"]["richTextSegment"])
            };

            foreach (var contract in contracts)
            {
                var properties = SchemaProperties(schema, contract.Item2);
                foreach (var property in SerializedProperties(contract.Item1))
                    Assert.True(properties.Contains(property.Name),
                        contract.Item1.FullName + "." + property.Name + " is missing from the JSON schema.");
            }
        }

        /// <summary>
        /// 获取类型中参与 JSON 序列化的公共属性。
        /// </summary>
        private static IEnumerable<PropertyInfo> SerializedProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x.GetIndexParameters().Length == 0 &&
                    x.GetCustomAttribute<JsonIgnoreAttribute>() == null);
        }

        /// <summary>
        /// 获取 schema contract 及其组合定义声明的属性名。
        /// </summary>
        private static HashSet<string> SchemaProperties(JObject schema, JToken contract)
        {
            var properties = new HashSet<string>(StringComparer.Ordinal);
            CollectSchemaProperties(schema, contract, properties, new HashSet<string>(StringComparer.Ordinal));
            return properties;
        }

        /// <summary>
        /// 递归收集 schema contract、引用和 allOf 中的属性名。
        /// </summary>
        private static void CollectSchemaProperties(JObject schema, JToken contract,
            HashSet<string> properties, HashSet<string> visited)
        {
            if (contract == null) return;
            var reference = (string)contract["$ref"];
            if (!string.IsNullOrEmpty(reference))
            {
                var name = reference.Substring(reference.LastIndexOf('/') + 1);
                if (!visited.Add(name)) return;
                CollectSchemaProperties(schema, schema["definitions"][name], properties, visited);
            }

            foreach (var property in (contract["properties"] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
                properties.Add(property.Name);
            foreach (var item in (contract["allOf"] as JArray) ?? Enumerable.Empty<JToken>())
                CollectSchemaProperties(schema, item, properties, visited);
        }
    }
}
