using System.Collections.Generic;
using System.Threading.Tasks;
using SmartCode;

namespace Bing.PdmGenerateDemo
{
    public class SampleDataSource : IDataSource
    {
        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// 名称
        /// </summary>
        public virtual string Name { get; private set; } = "Sample";

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="parameters">参数字典</param>
        public void Initialize(IDictionary<string, object> parameters)
        {
            this.Initialized = true;
        }

        public Task InitData()
        {
            return Task.CompletedTask;
        }
    }
}
