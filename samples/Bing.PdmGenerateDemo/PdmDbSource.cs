using System.Collections.Generic;
using System.Threading.Tasks;
using SmartCode;

namespace Bing.PdmGenerateDemo
{
    /// <summary>
    /// Pdm数据源
    /// </summary>
    public class PdmDbSource:IDataSource
    {
        

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// 名称
        /// </summary>
        public virtual string Name { get; private set; } = "Pdm";

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="parameters">参数字典</param>
        public void Initialize(IDictionary<string, object> parameters)
        {
            if (parameters != null)
            {
                if (parameters.Value("Name", out string name))
                {
                    Name = name;
                }
            }
            this.Initialized = true;
        }

        public async Task InitData()
        {
            throw new System.NotImplementedException();
        }
    }
}
