using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SmartCode;
using SmartCode.Generator.Entity;
using SmartCode.TemplateEngine;

namespace Bing.PdmGenerateDemo
{
    public class SampleBuildTask : IBuildTask
    {
        private IPluginManager _pluginManager;

        public SampleBuildTask(IPluginManager pluginManager)
        {
            _pluginManager = pluginManager;
        }

        public void Initialize(IDictionary<string, object> parameters)
        {
            Initialized = true;
        }

        public bool Initialized { get; private set; }
        public string Name { get; private set; } = "Sample";
        public async Task Build(BuildContext context)
        {
            var table = new Table();
            table.Description = "test";
            table.TypeName = "T";
            table.Columns = new[]
            {
                new Column()
                {
                    Name = "Name"
                },
                new Column()
                {
                    Name = "Agent"
                }
            };

            var filterTables = new[] { table };

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
