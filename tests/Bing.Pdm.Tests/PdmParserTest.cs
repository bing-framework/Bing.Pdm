using Bing.Pdm.Parser;
using Bing.Pdm.Reader;
using Bing.Utils.IO;
using Bing.Utils.Json;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Pdm.Tests
{
    public class PdmParserTest : TestBase
    {
        /// <summary>
        /// 分析器
        /// </summary>
        private IPdmParser _parser;

        /// <summary>
        /// 读取器
        /// </summary>
        private IPdmReader _reader;

        /// <summary>
        /// 文件路径
        /// </summary>
        private string _filePath = "D:\\test\\物流平台.pdm";

        public PdmParserTest(ITestOutputHelper output) : base(output)
        {
            _parser = new PdmParser();
            _reader = new PdmReader();
        }

        /// <summary>
        /// 测试 PDM信息
        /// </summary>
        [Fact]
        public void Test_Parser()
        {
            var result = _reader.ReadFromFile(_filePath);
            FileHelper.Write("D:\\test\\物流平台.json", result.ToJson());
            //Output.WriteLine(result.ToJson());
        }

        /// <summary>
        /// 测试 架构集合
        /// </summary>
        [Fact]
        public void Test_Reader_Schemas()
        {
            var result = _reader.ReadFromFile(_filePath);
            Output.WriteLine(result.Schemas.ToJson());
        }

        /// <summary>
        /// 测试 视图集合
        /// </summary>
        [Fact]
        public void Test_Reader_Views()
        {
            var result = _reader.ReadFromFile(_filePath);
            Output.WriteLine(result.Views.ToJson());
        }

        /// <summary>
        /// 测试 表集合
        /// </summary>
        [Fact]
        public void Test_Reader_Tables()
        {
            var result = _reader.ReadFromFile(_filePath);
            Output.WriteLine(result.Tables.ToJson());
        }

        /// <summary>
        /// 测试 包集合
        /// </summary>
        [Fact]
        public void Test_Reader_Packages()
        {
            var result = _reader.ReadFromFile(_filePath);
            Output.WriteLine(result.Packages.ToJson());
        }

        /// <summary>
        /// 测试 默认组集合
        /// </summary>
        [Fact]
        public void Test_Reader_DefaultGroups()
        {
            var result = _reader.ReadFromFile(_filePath);
            Output.WriteLine(result.DefaultGroups.ToJson());
        }

        /// <summary>
        /// 测试 物理图集合
        /// </summary>
        [Fact]
        public void Test_Reader_PhysicalDiagrams()
        {
            var result = _reader.ReadFromFile(_filePath);
            Output.WriteLine(result.PhysicalDiagrams.ToJson());
        }
    }
}
