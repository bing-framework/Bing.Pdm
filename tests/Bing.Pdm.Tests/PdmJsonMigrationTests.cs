using System;
using System.IO;
using System.Text;
using Bing.Pdm.Models;
using Bing.Pdm.Reader;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证 PDM JSON 格式迁移。
    /// </summary>
    public sealed class PdmJsonMigrationTests
    {
        /// <summary>
        /// 验证历史文件升级时保留未知字段和日期文本。
        /// </summary>
        [Fact]
        public void MigratesLegacyJsonWithoutDroppingData()
        {
            const string legacy = "{\"Id\":\"m\",\"Comment\":\"2026-10-04T00:00:00Z\",\"Tables\":[],\"Future\":{\"Flag\":true}}";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(legacy)))
            using (var output = new StringWriter())
            {
                new PdmJsonMigrator().Migrate(stream, output);
                Assert.True(stream.CanRead);
                Assert.Equal(1, (int)JObject.Parse(output.ToString())["SchemaVersion"]);
                Assert.True((bool)JObject.Parse(output.ToString())["Future"]["Flag"]);
                Assert.Contains("\"Comment\": \"2026-10-04T00:00:00Z\"", output.ToString());

                using (var repeated = new MemoryStream(Encoding.UTF8.GetBytes(output.ToString())))
                using (var repeatedOutput = new StringWriter())
                {
                    new PdmJsonMigrator().Migrate(repeated, repeatedOutput);
                    Assert.True(JToken.DeepEquals(JToken.Parse(output.ToString()), JToken.Parse(repeatedOutput.ToString())));
                }
            }
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(legacy)))
            {
                var model = new PdmJsonReader().Read(stream);
                Assert.Equal("2026-10-04T00:00:00Z", model.Comment);
                Assert.NotNull(model.Lookup);
                Assert.True(stream.CanRead);
            }
        }

        /// <summary>
        /// 验证无效和未来版本被明确拒绝。
        /// </summary>
        [Theory]
        [InlineData("{\"Id\":\"m\",\"Tables\":[],\"SchemaVersion\":2}")]
        [InlineData("{\"Id\":\"m\",\"Tables\":[],\"SchemaVersion\":\"1\"}")]
        [InlineData("{\"Id\":\"m\",\"Tables\":[],\"SchemaVersion\":-1}")]
        [InlineData("{\"Id\":\"m\",\"Tables\":[],\"SchemaVersion\":999999999999999999999}")]
        [InlineData("{\"Id\":\"m\",\"Tables\":[1]}")]
        [InlineData("{\"Id\":\"m\",\"Tables\":[{\"Columns\":[null]}]}")]
        [InlineData("{\"Id\":\"m\",\"Packages\":[{\"Tables\":{}}]}")]
        [InlineData("{\"Id\":\"m\",\"References\":[{\"ParentTableAddress\":42}]}")]
        [InlineData("{\"Id\":\"m\",\"Tables\":[]")]
        public void RejectsUnsupportedOrDamagedJson(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                Assert.ThrowsAny<Exception>(() => new PdmJsonReader().Read(stream));
        }
        /// <summary>
        /// 验证已知字段损坏时迁移与读取均失败且流保持打开。
        /// </summary>
        [Theory]
        [InlineData("{\"Tables\":[{\"Columns\":[{\"Mandatory\":\"true\"}]}]}")]
        [InlineData("{\"Tables\":[{\"Columns\":[{\"DataType\":123}]}]}")]
        [InlineData("{\"Tables\":[{\"Indexes\":[{\"Unique\":1}]}]}")]
        [InlineData("{\"Tables\":[{\"Indexes\":[{\"IndexColumns\":[{\"ColumnId\":false}]}]}]}")]
        [InlineData("{\"PhysicalDiagrams\":[{\"Symbols\":[{\"Rect\":{\"X1\":\"1\"}}]}]}")]
        [InlineData("{\"PhysicalDiagrams\":[{\"Symbols\":[{\"Points\":[{\"X\":2147483648}]}]}]}")]
        [InlineData("{\"PhysicalDiagrams\":[{\"Symbols\":[{\"RichTextSegments\":[{\"Bold\":0}]}]}]}")]
        [InlineData("{\"Diagnostics\":[{\"Message\":{}}]}")]
        [InlineData("{\"TargetModels\":[{\"EmbeddedObjects\":[{\"Kind\":[]}]}]}")]
        [InlineData("{\"Replications\":[{\"RawAttributes\":{\"Custom\":42}}]}")]
        [InlineData("{\"Owners\":{}}")]
        [InlineData("{\"Schemas\":[{\"Code\":false}]}")]
        [InlineData("{\"Tables\":null}")]
        [InlineData("{\"CreationDate\":\"invalid-date\"}")]
        [InlineData("{\"Tables\":[],\"Tables\":[{}]}")]
        public void RejectsDamagedKnownFieldsInBothEntryPoints(string fragment)
        {
            var json = "{\"Id\":\"m\",\"Packages\":[]," + fragment.Substring(1);
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            using var output = new StringWriter();
            Assert.ThrowsAny<Exception>(() => new PdmJsonMigrator().Migrate(input, output));
            Assert.Empty(output.ToString());
            Assert.True(input.CanRead);
            input.Position = 0;
            Assert.ThrowsAny<Exception>(() => new PdmJsonReader().Read(input));
            Assert.True(input.CanRead);
        }

        /// <summary>
        /// 验证未知扩展不参与固定契约校验。
        /// </summary>
        [Fact]
        public void PreservesUnknownFieldsInsideKnownObjects()
        {
            const string json = "{\"Id\":\"m\",\"Tables\":[{\"Id\":\"t\",\"Future\":{\"Columns\":42},"
                + "\"Columns\":[{\"Id\":\"c\",\"DataType\":null}]}]}";
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));
            using var output = new StringWriter();
            new PdmJsonMigrator().Migrate(input, output);
            Assert.Equal(42, (int)JObject.Parse(output.ToString())["Tables"][0]["Future"]["Columns"]);
            input.Position = 0;
            Assert.Null(new PdmJsonReader().Read(input).Tables[0].Columns[0].DataType);
        }
    }
}
