using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartCode;
using SmartCode.TemplateEngine;

namespace Bing.PdmGenerateDemo
{
    /// <summary>
    /// 使用 PDM 表执行 SmartCode 模板生成。
    /// </summary>
    public class SampleBuildTask : IBuildTask
    {
        /// <summary>
        /// SmartCode 插件管理器。
        /// </summary>
        private IPluginManager _pluginManager;

        /// <summary>
        /// 初始化一个 <see cref="SampleBuildTask"/> 类型的实例。
        /// </summary>
        /// <param name="pluginManager">SmartCode 插件管理器。</param>
        public SampleBuildTask(IPluginManager pluginManager)
        {
            _pluginManager = pluginManager;
        }

        /// <inheritdoc />
        public void Initialize(IDictionary<string, object> parameters)
        {
            Initialized = true;
        }

        /// <inheritdoc />
        public bool Initialized { get; private set; }
        /// <inheritdoc />
        public string Name { get; private set; } = "Sample";
        /// <inheritdoc />
        public async Task Build(BuildContext context)
        {
            var dataSource = _pluginManager.Resolve<IDataSource>(context.Project.DataSource.Name) as PdmDbSource
                ?? throw new InvalidOperationException("The selected SmartCode data source is not a PDM source.");
            await dataSource.InitData();
            var filterTables = dataSource.Tables.ToArray();
            if (filterTables.Length == 0)
                throw new InvalidOperationException("The PDM model contains no tables to generate.");

            context.SetCurrentAllTable(filterTables);

            foreach (var _table in filterTables)
            {
                context.SetCurrentTable(_table);
                context.Result = await _pluginManager.Resolve<ITemplateEngine>(context.Build.TemplateEngine.Name).Render(context);
                await _pluginManager.Resolve<IOutput>(context.Build.Output.Type).Output(context);
            }
        }
    }
}
