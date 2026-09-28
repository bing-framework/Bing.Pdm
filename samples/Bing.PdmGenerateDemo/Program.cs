using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartCode;
using SmartCode.App;
using SmartCode.Configuration;

namespace Bing.PdmGenerateDemo
{
    /// <summary>
    /// SmartCode PDM 生成示例入口。
    /// </summary>
    class Program
    {
        /// <summary>
        /// 执行 SmartCode 生成示例。
        /// </summary>
        /// <param name="args">命令行参数。</param>
        /// <returns>进程退出代码。</returns>
        static async Task<int> Main(string[] args)
        {
            if (args.Any(argument => argument == "--help" || argument == "-h"))
            {
                PrintUsage();
                return 0;
            }

            try
            {
                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(appDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .Build();

                var pdmPath = GetOption(args, "--pdm") ?? configuration["PdmFilePath"];
                var outputSetting = GetOption(args, "--output") ?? configuration["OutputPath"] ?? "Generated";
                if (string.IsNullOrWhiteSpace(pdmPath))
                    throw new ArgumentException("Provide a PDM file with --pdm or set PdmFilePath in appsettings.json.");

                pdmPath = Path.GetFullPath(pdmPath, Environment.CurrentDirectory);
                if (!File.Exists(pdmPath))
                    throw new FileNotFoundException($"PDM file was not found: '{pdmPath}'.", pdmPath);

                var outputPath = Path.GetFullPath(outputSetting, Environment.CurrentDirectory);
                var smartCodeOptions = configuration.GetSection("SmartCode").Get<SmartCodeOptions>()
                    ?? throw new InvalidOperationException("The SmartCode section is missing from appsettings.json.");
                var pdmSourcePlugin = smartCodeOptions.Plugins.SingleOrDefault(
                    plugin => plugin.ImplTypeName == typeof(PdmDbSource).FullName)
                    ?? throw new InvalidOperationException("The PdmDbSource plugin is missing from the SmartCode section.");
                pdmSourcePlugin.Parameters ??= new Dictionary<string, object>();
                pdmSourcePlugin.Parameters["FilePath"] = pdmPath;

                var services = smartCodeOptions.Services;
                services.AddSingleton(smartCodeOptions);
                services.AddSingleton<IPluginManager, PluginManager>();
                services.AddSingleton<IProjectBuilder, ProjectBuilder>();
                services.AddLogging();

                foreach (var plugin in smartCodeOptions.Plugins)
                {
                    var pluginType = Assembly.Load(plugin.AssemblyName).GetType(plugin.TypeName);
                    if (pluginType == null)
                        throw new SmartCodeException($"Plugin.Type:{plugin.TypeName} can not find!");

                    var implType = Assembly.Load(plugin.ImplAssemblyName).GetType(plugin.ImplTypeName);
                    if (implType == null)
                        throw new SmartCodeException($"Plugin.ImplType:{plugin.ImplTypeName} can not find!");

                    if (!pluginType.IsAssignableFrom(implType))
                        throw new SmartCodeException($"Plugin.ImplType:{implType.FullName} can not implement Plugin.Type:{pluginType.FullName}!");

                    services.AddSingleton(pluginType, implType);
                }

                var project = new Project
                {
                    Module = "generated",
                    DataSource = new DataSource
                    {
                        Name = "Pdm",
                        Parameters = new Dictionary<string, object> { { "FilePath", pdmPath } }
                    },
                    Output = new Output { Type = "File", Path = outputPath },
                    BuildTasks = new Dictionary<string, Build>(),
                    TableFilter = new TableFilter()
                };

                var clearDir = new Build
                {
                    Type = "Clear",
                    Parameters = new Dictionary<string, object> { { "Dirs", "generated.entity" } }
                };
                var entity = new Build
                {
                    Type = "Sample",
                    Module = "entity",
                    Output = new Output
                    {
                        Path = "{{Project.Module}}.{{Build.Module}}",
                        Name = "{{Items.CurrentTable.ConvertedName}}",
                        Extension = ".java"
                    },
                    TemplateEngine = new TemplateEngine
                    {
                        Root = "Java",
                        Name = "Handlebars",
                        Path = "Entity.hbs"
                    }
                };

                project.BuildTasks.Add("ClearDir", clearDir);
                project.BuildTasks.Add("Entity", entity);
                services.AddSingleton(project);

                using (var serviceProvider = services.BuildServiceProvider())
                {
                    var projectBuilder = serviceProvider.GetRequiredService<IProjectBuilder>();
                    await projectBuilder.Build();
                }

                if (!Directory.Exists(outputPath)
                    || !Directory.EnumerateFiles(outputPath, "*.java", SearchOption.AllDirectories).Any())
                    throw new InvalidOperationException("SmartCode completed without generating any Java entity files.");

                Console.WriteLine($"Generated SmartCode output to '{outputPath}'.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        /// <summary>
        /// 获取命令行选项值。
        /// </summary>
        /// <param name="args">命令行参数。</param>
        /// <param name="name">选项名称。</param>
        /// <returns>选项值；未指定时返回 <see langword="null"/>。</returns>
        private static string GetOption(string[] args, string name)
        {
            for (var index = 0; index < args.Length; index++)
            {
                if (args[index] != name)
                    continue;

                if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Option {name} requires a value.");

                return args[index + 1];
            }

            return null;
        }

        /// <summary>
        /// 输出命令行用法。
        /// </summary>
        private static void PrintUsage()
        {
            Console.WriteLine("Usage: Bing.PdmGenerateDemo [--pdm <file>] [--output <directory>]");
            Console.WriteLine("PDM and output paths can also be set with PdmFilePath and OutputPath in appsettings.json.");
        }
    }
}
