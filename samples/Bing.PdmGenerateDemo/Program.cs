using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartCode;
using SmartCode.App;
using SmartCode.Configuration;

namespace Bing.PdmGenerateDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var APP_SETTINGS_PATH = "appsettings.json";
            var SMARTCODE_KEY = "SmartCode";
            var AppDirectory = AppDomain.CurrentDomain.BaseDirectory;

            Task.Factory.StartNew(async () =>
            {
                try
                {
                    #region Reader Config
                    var appSettingsbuilder = new ConfigurationBuilder()
                            .SetBasePath(AppDirectory)
                            .AddJsonFile(APP_SETTINGS_PATH, false, true);
                    var configuration = appSettingsbuilder.Build();
                    var smartCodeOptions = configuration.GetSection(SMARTCODE_KEY).Get<SmartCodeOptions>();
                    #endregion

                    #region Auto Inject
                    var services = smartCodeOptions.Services;
                    services.AddSingleton(smartCodeOptions);
                    services.AddSingleton<IPluginManager, PluginManager>();
                    services.AddSingleton<IProjectBuilder, ProjectBuilder>();
                    services.AddLogging();
                    #endregion

                    #region Add Plugs
                    foreach (var plugin in smartCodeOptions.Plugins)
                    {
                        var pluginType = Assembly.Load(plugin.AssemblyName).GetType(plugin.TypeName);
                        if (pluginType == null)
                        {
                            throw new SmartCodeException($"Plugin.Type:{plugin.TypeName} can not find!");
                        }
                        var implType = Assembly.Load(plugin.ImplAssemblyName).GetType(plugin.ImplTypeName);
                        if (implType == null)
                        {
                            throw new SmartCodeException($"Plugin.ImplType:{plugin.ImplTypeName} can not find!");
                        }
                        if (!pluginType.IsAssignableFrom(implType))
                        {
                            throw new SmartCodeException($"Plugin.ImplType:{implType.FullName} can not Impl Plugin.Type：{pluginType.FullName}!");
                        }
                        services.AddSingleton(pluginType, implType);
                    }
                    #endregion

                    #region Project
                    var project = new Project()
                    {
                        DataSource = new DataSource
                        {
                            Name = "Sample"
                        },
                        Output = new Output()
                        {
                            Type = "File",
                            Path = "I:\\Builder"
                        },
                        BuildTasks = new Dictionary<string, Build>(),
                        TableFilter = new TableFilter(),
                    };

                    var clearDir = new Build
                    {
                        Type = "Clear",
                        Parameters = new Dictionary<String, object>()
                        {
                            { "Dirs", "." }
                        }
                    };
                    var entity = new Build
                    {
                        Type = "Sample",
                        Module = "entity",
                        Output = new Output(),
                        TemplateEngine = new TemplateEngine()
                    };

                    entity.Output.Path = "{{Project.Module}}.{{Build.Module}}";
                    entity.Output.Name = "{{Items.CurrentTable.ConvertedName}}";
                    entity.Output.Extension = ".java";

                    entity.TemplateEngine.Root = "Java";
                    entity.TemplateEngine.Name = "Razor";
                    entity.TemplateEngine.Path = "Entity.cshtml";

                    project.BuildTasks.Add("ClearDir", clearDir);
                    project.BuildTasks.Add("Entity", entity);
                    services.AddSingleton(project);
                    #endregion

                    #region Build Project
                    var serviceProvider = services.BuildServiceProvider();
                    var projectBuilder = serviceProvider.GetRequiredService<IProjectBuilder>();

                    await projectBuilder.Build();
                    #endregion
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
            Console.WriteLine("来源隔壁老萌的新手大礼包");
            Console.ReadLine();
        }
    }
}
