using Xunit.Abstractions;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// ²âÊÔ»ùÀà
    /// </summary>
    public class TestBase
    {
        protected ITestOutputHelper Output;

        public TestBase(ITestOutputHelper output)
        {
            Output = output;
        }
    }
}
