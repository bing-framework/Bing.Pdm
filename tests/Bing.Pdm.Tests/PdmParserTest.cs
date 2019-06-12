using Bing.Pdm.Parser;
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
        /// 文件路径
        /// </summary>
        private string _filePath = "D:\\物流平台.pdm";

        public PdmParserTest(ITestOutputHelper output) : base(output)
        {
            _parser = new PdmParser();
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
    }
}
