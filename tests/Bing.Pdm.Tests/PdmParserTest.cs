using Bing.Pdm.Parser;
using Bing.Pdm.Reader;
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
        /// 测试PDM分析
        /// </summary>
        [Fact]
        public void Test_Parser()
        {
            var result = _parser.Parser(_filePath);
            Output.WriteLine(result.ToJson());
        }

        /// <summary>
        /// 测试 架构集合
        /// </summary>
        [Fact]
        public void Test_Reader_Schema()
        {
            var result = _reader.ReadFromFile(_filePath);
            Output.WriteLine(result.Schemas.ToJson());
        }

        /// <summary>
        /// 测试 视图集合
        /// </summary>
        [Fact]
        public void Test_Reader_View()
        {
            var result = _reader.ReadFromFile(_filePath);
            Output.WriteLine(result.Views.ToJson());
        }

        /// <summary>
        /// 测试 表集合
        /// </summary>
        [Fact]
        public void Test_Reader_Table()
        {
            var result = _reader.ReadFromFile(_filePath);
            Output.WriteLine(result.Tables.ToJson());
        }

        /// <summary>
        /// 测试 包集合
        /// </summary>
        [Fact]
        public void Test_Reader_Package()
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
    }
}
