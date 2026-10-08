using System.Text;
using System.Text.RegularExpressions;
using Bing.Pdm;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Reader;
using Bing.Pdm.Models;
using Newtonsoft.Json;
using Microsoft.Win32;

namespace Bing.Pdm.Tool;

/// <summary>
/// 提供 PDM 命令行工具的入口。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 解析命令行参数并执行导出或实体生成。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>进程退出码。</returns>
    private static int Main(string[] args)
    {
        if (args.Length < 3 || (args[0] != "export" && args[0] != "generate" && args[0] != "migrate" && args[0] != "diff") ||
            (args[0] == "migrate" && args.Length != 3))
        {
            Console.Error.WriteLine("Usage: pdm export <input.pdm|input.json> <output-dir> [json,md,html,svg,xlsx,docx] [en|zh] [--input-format pdm|json] [--workspace <manifest>] [--diagram-style simplified|powerdesigner]");
            Console.Error.WriteLine("       pdm generate <input.pdm|input.json> <output-dir> [namespace] [--map <dbms|*>:<database-type>=<C#-type>]... [--fallback-type <C#-type>] [--input-format pdm|json]");
            Console.Error.WriteLine("       pdm migrate <input.json> <output.json>");
            Console.Error.WriteLine("       pdm diff <before.pdm|json> <after.pdm|json> <output-dir> [json,md,html] [--fail-on-change] [--fail-on-incomplete] [--before-workspace <manifest>] [--after-workspace <manifest>]");
            return 2;
        }

        try
        {
            if (args[0] == "migrate")
            {
                EnsureInputIsNotOutput(args[1], args[2]);
                using var input = File.OpenRead(args[1]);
                using var output = new StringWriter();
                new PdmJsonMigrator().Migrate(input, output);
                var outputPath = Path.GetFullPath(args[2]);
                using var batch = new PdmOutputBatch(Path.GetDirectoryName(outputPath)!, new[] { Path.GetFileName(outputPath) });
                File.WriteAllText(batch.StagePath(Path.GetFileName(outputPath)), output.ToString(), new UTF8Encoding(false));
                batch.Commit();
                return 0;
            }
            if (args[0] == "diff") return Diff(args);
            var withoutWorkspace = WithoutWorkspace(args, out var workspacePath);
            var withoutStyle = WithoutDiagramStyle(withoutWorkspace, out var diagramStyle);
            using var fontMetrics = diagramStyle.Style == PdmDiagramStyle.PowerDesigner && OperatingSystem.IsWindows()
                ? new WindowsDiagramFontMetrics() : null;
            if (fontMetrics != null) diagramStyle.MeasureText = fontMetrics.Measure;
            var options = WithoutInputFormat(withoutStyle, out var inputFormat);
            var workspace = workspacePath == null ? null : new PdmWorkspaceReader().ReadFromFile(workspacePath);
            var modelKey = workspace?.FindModelKeyByPath(args[1]);
            if (workspace != null && modelKey == null)
                throw new ArgumentException("Input must identify exactly one model in the workspace manifest.");
            var model = workspace == null ? ReadModel(args[1], inputFormat) : workspace.Models[modelKey];
            if (args[0] == "export")
                Export(model, args[1], args[2], options.Length > 3 ? options[3] : "json,md,html", ParseLanguage(options.Length > 4 ? options[4] : "en"), workspace, modelKey, diagramStyle);
            else
            {
                var hasNamespace = options.Length > 3 && !options[3].StartsWith("--", StringComparison.Ordinal);
                var targetNamespace = hasNamespace ? options[3] : "Generated.Entities";
                var optionIndex = hasNamespace ? 4 : 3;
                var generation = EntityGenerator.GenerateWithDiagnostics(model.AllTables, args[2], targetNamespace,
                    model.DbmsCode, model.DbmsName, ReadTypeMappings(options, optionIndex), null, args[1]);
                foreach (var diagnostic in generation.Diagnostics)
                    Console.Error.WriteLine($"{diagnostic.Code} [{diagnostic.Table}.{diagnostic.Column}]: {diagnostic.Message}");
                if (generation.Diagnostics.Any(x => x.Code == "TYPE_ERROR")) return 1;
            }

            foreach (var diagnostic in workspace == null ? model.Diagnostics : workspace.GetDiagnostics(modelKey))
                Console.Error.WriteLine($"{diagnostic.Code} [{diagnostic.SourceId}]: {diagnostic.Message}");
            foreach (var diagnostic in diagramStyle.Diagnostics)
                Console.Error.WriteLine($"{diagnostic.Code} [{diagnostic.SourceId}]: {diagnostic.Message}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Xml.XmlException or JsonException or ArgumentException or InvalidOperationException or DecoderFallbackException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(exception.Message);
            return args[0] == "diff" && exception is ArgumentException ? 2 : 1;
        }
    }

    /// <summary>
    /// 解析差异比较命令并写入报告。
    /// </summary>
    private static int Diff(string[] args)
    {
        if (args.Length < 4) throw new ArgumentException("Expected diff <before> <after> <output-dir>.");
        args = WithoutDiffWorkspaces(args, out var beforeManifest, out var afterManifest);
        var fail = args.Contains("--fail-on-change", StringComparer.Ordinal);
        var failOnIncomplete = args.Contains("--fail-on-incomplete", StringComparer.Ordinal);
        var values = args.Where(x => x != "--fail-on-change" && x != "--fail-on-incomplete").ToArray();
        if (values.Length > 5) throw new ArgumentException("Too many diff arguments.");
        var formats = ParseExportFormats(values.Length == 5 ? values[4] : "json,md,html");
        if (formats.Any(x => x is not ("json" or "md" or "html")))
            throw new ArgumentException("Diff reports support json, md and html only.");
        var beforeWorkspace = beforeManifest == null ? null : new PdmWorkspaceReader().ReadFromFile(beforeManifest);
        var afterWorkspace = afterManifest == null ? null : new PdmWorkspaceReader().ReadFromFile(afterManifest);
        var beforeKey = beforeWorkspace?.FindModelKeyByPath(values[1]);
        var afterKey = afterWorkspace?.FindModelKeyByPath(values[2]);
        if ((beforeWorkspace != null && beforeKey == null) || (afterWorkspace != null && afterKey == null))
            throw new InvalidDataException("Diff inputs must identify named models in their corresponding workspace manifests.");
        var before = beforeWorkspace == null ? ReadModel(values[1], null) : beforeWorkspace.Models[beforeKey!];
        var after = afterWorkspace == null ? ReadModel(values[2], null) : afterWorkspace.Models[afterKey!];
        var result = new PdmModelComparer().Compare(before, after, new PdmCompareOptions
        { BeforeWorkspace = beforeWorkspace, AfterWorkspace = afterWorkspace,
            BeforeModelKey = beforeKey, AfterModelKey = afterKey });
        var basename = Path.GetFileNameWithoutExtension(values[1]) + "-to-" + Path.GetFileNameWithoutExtension(values[2]);
        foreach (var format in formats)
            foreach (var input in new[] { values[1], values[2] })
                EnsureInputIsNotOutput(input, Path.Combine(values[3], basename + "." + format));
        using var batch = new PdmOutputBatch(values[3], formats.Select(x => basename + "." + x));
        var exporter = new PdmDiffExporter();
        foreach (var format in formats)
        {
            var path = Path.Combine(values[3], basename + "." + format);
            using var writer = new StreamWriter(batch.StagePath(Path.GetFileName(path)), false, new UTF8Encoding(false));
            exporter.Write(result, format == "json" ? PdmExportFormat.Json :
                format == "md" ? PdmExportFormat.Markdown : PdmExportFormat.Html, writer);
        }
        batch.Commit();
        foreach (var format in formats) Console.WriteLine(Path.Combine(values[3], basename + "." + format));
        if (result.Incomplete)
            Console.Error.WriteLine("INCOMPLETE_COMPARISON: inspect ValidationIssues and Ambiguous changes in the report.");
        if (failOnIncomplete && result.Incomplete) return 1;
        return fail && result.Changes.Count > 0 ? 3 : 0;
    }

    /// <summary>
    /// 提取差异比较两侧的工作区清单。
    /// </summary>
    private static string[] WithoutDiffWorkspaces(string[] args, out string? before, out string? after)
    {
        before = null; after = null;
        var values = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag is not ("--before-workspace" or "--after-workspace")) { values.Add(flag); continue; }
            if (i < 4 || ++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal) ||
                (flag == "--before-workspace" ? before != null : after != null))
                throw new ArgumentException("Expected one " + flag + " <manifest> option.");
            if (flag == "--before-workspace") before = args[i]; else after = args[i];
        }
        return values.ToArray();
    }

    /// <summary>
    /// 提取显式工作区清单选项。
    /// </summary>
    private static string[] WithoutWorkspace(string[] args, out string? manifest)
    {
        manifest = null;
        var values = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] != "--workspace") { values.Add(args[i]); continue; }
            if (i < 3 || manifest != null || ++i >= args.Length)
                throw new ArgumentException("Expected one --workspace <manifest> option.");
            manifest = args[i];
        }
        return values.ToArray();
    }

    /// <summary>
    /// 提取物理图渲染风格选项。
    /// </summary>
    private static string[] WithoutDiagramStyle(string[] args, out PdmDiagramRenderOptions options)
    {
        options = new PdmDiagramRenderOptions();
        var values = new List<string>();
        var seen = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] != "--diagram-style") { values.Add(args[i]); continue; }
            if (args[0] != "export" || seen || ++i >= args.Length)
                throw new ArgumentException("Expected one --diagram-style simplified|powerdesigner option on export.");
            seen = true;
            options.Style = args[i].ToLowerInvariant() switch
            {
                "simplified" => PdmDiagramStyle.Simplified,
                "powerdesigner" => PdmDiagramStyle.PowerDesigner,
                _ => throw new ArgumentException("Diagram style must be simplified or powerdesigner.")
            };
            if (options.Style == PdmDiagramStyle.PowerDesigner && OperatingSystem.IsWindows())
                options.FontAvailable = IsInstalledFont;
        }
        return values.ToArray();
    }

    /// <summary>
    /// 检查 Windows 已安装字体名称。
    /// </summary>
    private static bool IsInstalledFont(string family)
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (string.Equals(family, "新宋体", StringComparison.OrdinalIgnoreCase)) family = "NSimSun";
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
        return key != null && key.GetValueNames().Any(x =>
            x.Split('(')[0].Split('&').Any(name =>
                string.Equals(name.Trim(), family, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// 提取输入格式选项。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="format">显式指定的输入格式。</param>
    /// <returns>移除输入格式选项后的参数。</returns>
    private static string[] WithoutInputFormat(string[] args, out string? format)
    {
        format = null;
        var values = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] != "--input-format") { values.Add(args[i]); continue; }
            if (i < 3 || format != null || ++i >= args.Length)
                throw new ArgumentException("Expected one --input-format pdm|json option.");
            format = args[i];
        }
        return values.ToArray();
    }

    /// <summary>
    /// 按指定格式或文件扩展名读取模型。
    /// </summary>
    /// <param name="input">输入文件路径。</param>
    /// <param name="format">显式指定的格式。</param>
    /// <returns>读取的 PDM 模型。</returns>
    private static Bing.Pdm.Models.PdmInfo ReadModel(string input, string? format)
    {
        var selected = format ?? Path.GetExtension(input).TrimStart('.');
        if (selected.Equals("pdm", StringComparison.OrdinalIgnoreCase)) return new PdmReader().ReadFromFile(input);
        if (selected.Equals("json", StringComparison.OrdinalIgnoreCase)) return new PdmJsonReader().ReadFromFile(input);
        throw new InvalidDataException("Input format must be pdm or json; use --input-format for files without a supported extension.");
    }

    /// <summary>
    /// 按指定格式导出 PDM 模型。
    /// </summary>
    /// <param name="model">已读取的 PDM 模型。</param>
    /// <param name="input">输入文件路径。</param>
    /// <param name="output">输出目录。</param>
    /// <param name="formats">逗号分隔的导出格式。</param>
    /// <param name="language">导出内容语言。</param>
    private static void Export(Bing.Pdm.Models.PdmInfo model, string input, string output, string formats, PdmExportLanguage language,
        PdmWorkspace? workspace = null, string? modelKey = null, PdmDiagramRenderOptions? diagramStyle = null)
    {
        var names = ParseExportFormats(formats);
        var diagrams = model.AllPhysicalDiagrams.ToArray();
        if (names.Contains("svg") && diagrams.Length == 0)
            throw new InvalidOperationException("The model has no physical diagrams to export.");
        var basename = Path.GetFileNameWithoutExtension(input);
        var plannedNames = new List<string>();
        var plannedDiagrams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var format in names)
        {
            if (format == "svg")
            {
                foreach (var diagram in diagrams)
                {
                    var path = UniqueDiagramPath(output, basename,
                        FileNamePart(diagram.Code ?? diagram.Name ?? diagram.Id), plannedDiagrams);
                    EnsureInputIsNotOutput(input, path);
                    plannedNames.Add(Path.GetFileName(path));
                }
                continue;
            }
            var extension = format switch
            {
                "xlsx" => "xlsx",
                "docx" => "docx",
                "md" => "md",
                "html" => "html",
                _ => "json"
            };
            EnsureInputIsNotOutput(input, Path.Combine(output, basename + "." + extension));
            plannedNames.Add(basename + "." + extension);
        }
        using var batch = new PdmOutputBatch(output, plannedNames);
        var exporter = new PdmExporter(language);
        var officeExporter = new PdmOfficeExporter(language);
        var diagramPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var format in names)
        {
            if (format == "svg")
            {
                foreach (var diagram in diagrams)
                {
                    var suffix = FileNamePart(diagram.Code ?? diagram.Name ?? diagram.Id);
                    var path = UniqueDiagramPath(output, basename, suffix, diagramPaths);
                    using var svg = new StreamWriter(batch.StagePath(Path.GetFileName(path)), false, new UTF8Encoding(false));
                    if (workspace == null) exporter.WriteDiagram(model, diagram, svg, diagramStyle);
                    else exporter.WriteDiagram(workspace, modelKey!, diagram, svg, diagramStyle);
                }
                continue;
            }
            if (format is "xlsx" or "excel" or "docx" or "word")
            {
                var extension = format is "xlsx" or "excel" ? "xlsx" : "docx";
                var path = Path.Combine(output, basename + "." + extension);
                using var stream = File.Create(batch.StagePath(Path.GetFileName(path)));
                if (extension == "xlsx")
                {
                    if (workspace == null) officeExporter.WriteExcel(model, stream);
                    else officeExporter.WriteExcel(workspace, modelKey!, stream);
                }
                else
                {
                    if (workspace == null) officeExporter.WriteWord(model, stream);
                    else officeExporter.WriteWord(workspace, modelKey!, stream);
                }
                continue;
            }
            var textFormat = format switch
            {
                "json" => PdmExportFormat.Json,
                "md" or "markdown" => PdmExportFormat.Markdown,
                "html" => PdmExportFormat.Html,
                _ => throw new ArgumentException($"Unknown export format: {format}")
            };
            var textExtension = textFormat == PdmExportFormat.Markdown ? "md" : textFormat.ToString().ToLowerInvariant();
            var textPath = Path.Combine(output, basename + "." + textExtension);
            using var writer = new StreamWriter(batch.StagePath(Path.GetFileName(textPath)), false, new UTF8Encoding(false));
            if (workspace == null) exporter.Write(model, textFormat, writer, diagramStyle);
            else exporter.Write(workspace, modelKey!, textFormat, writer, diagramStyle);
        }
        batch.Commit();
        foreach (var name in plannedNames) Console.WriteLine(Path.Combine(output, name));
    }

    /// <summary>
    /// 拒绝将输出写入输入文件。
    /// </summary>
    /// <param name="input">输入文件路径。</param>
    /// <param name="output">待写入的文件路径。</param>
    internal static void EnsureInputIsNotOutput(string input, string output)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), comparison))
            throw new InvalidOperationException("The output would overwrite the input file.");
    }

    /// <summary>
    /// 解析并去重导出格式名称。
    /// </summary>
    /// <param name="formats">逗号分隔的格式名称。</param>
    /// <returns>规范化后的格式名称。</returns>
    private static List<string> ParseExportFormats(string formats)
    {
        var names = (formats ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length == 0) throw new ArgumentException("At least one export format is required.");
        var formatsToWrite = new List<string>();
        foreach (var name in names)
        {
            var format = name.ToLowerInvariant() switch
            {
                "json" => "json",
                "md" or "markdown" => "md",
                "html" => "html",
                "svg" => "svg",
                "xlsx" or "excel" => "xlsx",
                "docx" or "word" => "docx",
                _ => throw new ArgumentException($"Unknown export format: {name}")
            };
            if (!formatsToWrite.Contains(format, StringComparer.Ordinal))
                formatsToWrite.Add(format);
        }
        return formatsToWrite;
    }

    /// <summary>
    /// 生成不冲突的物理图 SVG 输出路径。
    /// </summary>
    /// <param name="output">输出目录。</param>
    /// <param name="basename">输入文件基名。</param>
    /// <param name="suffix">物理图名称后缀。</param>
    /// <param name="usedPaths">已使用的输出路径集合。</param>
    /// <returns>可用的 SVG 输出路径。</returns>
    private static string UniqueDiagramPath(string output, string basename, string suffix, ISet<string> usedPaths)
    {
        var path = Path.Combine(output, basename + "-" + suffix + ".svg");
        var counter = 2;
        while (!usedPaths.Add(path))
        {
            path = Path.Combine(output, basename + "-" + suffix + "-" + counter + ".svg");
            counter++;
        }
        return path;
    }

    /// <summary>
    /// 解析导出语言选项。
    /// </summary>
    /// <param name="language">语言名称。</param>
    /// <returns>对应的导出语言。</returns>
    private static PdmExportLanguage ParseLanguage(string language) => language.ToLowerInvariant() switch
    {
        "zh" or "zh-cn" or "chinese" => PdmExportLanguage.Chinese,
        "en" or "en-us" or "english" => PdmExportLanguage.English,
        _ => throw new ArgumentException("Language must be en or zh.")
    };

    /// <summary>
    /// 解析命令行中的类型映射选项。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="index">开始读取选项的索引。</param>
    /// <returns>配置完成的类型映射器。</returns>
    private static CSharpTypeMapper ReadTypeMappings(string[] args, int index)
    {
        var mapper = new CSharpTypeMapper();
        while (index < args.Length)
        {
            if (args[index] == "--fallback-type" && index + 1 < args.Length)
            {
                mapper.RegisterFallback(args[index + 1]);
                index += 2;
                continue;
            }
            if (args[index] != "--map" || index + 1 >= args.Length)
                throw new ArgumentException("Expected --map <dbms|*>:<database-type>=<C#-type> or --fallback-type <C#-type>.");
            var mapping = args[index + 1];
            var separator = mapping.IndexOf(':');
            var assignment = mapping.IndexOf('=', separator + 1);
            if (separator <= 0 || assignment <= separator + 1 || assignment == mapping.Length - 1)
                throw new ArgumentException("Expected --map <dbms|*>:<database-type>=<C#-type>.");
            var dbms = mapping.Substring(0, separator);
            var databaseType = mapping.Substring(separator + 1, assignment - separator - 1);
            var clrType = mapping.Substring(assignment + 1);
            mapper.RegisterMapping(databaseType, clrType, dbms == "*" ? null : dbms);
            index += 2;
        }
        return mapper;
    }

    /// <summary>
    /// 将名称转换为合法的文件名片段。
    /// </summary>
    /// <param name="value">待转换的名称。</param>
    /// <returns>可用于文件名的安全片段。</returns>
    private static string FileNamePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string((value ?? "diagram").Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "diagram" : safe;
    }
}

/// <summary>
/// 根据 PDM 表生成 C# 实体源文件。
/// </summary>
public static class EntityGenerator
{
    /// <summary>
    /// 保存不能直接用作 C# 标识符的关键字。
    /// </summary>
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    };

    /// <summary>
    /// 判断名称是否为 C# 保留标识符。
    /// </summary>
    /// <param name="name">待检查的标识符。</param>
    /// <returns>名称为保留标识符时返回 true，否则返回 false。</returns>
    internal static bool IsReservedIdentifier(string name) => Keywords.Contains(name) || name == "var";

    /// <summary>
    /// 生成指定表对应的实体文件。
    /// </summary>
    /// <param name="tables">待生成的 PDM 表。</param>
    /// <param name="output">实体文件输出目录。</param>
    /// <param name="targetNamespace">生成类型使用的命名空间。</param>
    /// <param name="dbmsCode">PDM 数据库管理系统代码。</param>
    /// <param name="dbmsName">PDM 数据库管理系统名称。</param>
    /// <param name="typeMapper">可选的类型映射器。</param>
    /// <param name="template">可选的实体模板。</param>
    /// <remarks>
    /// 存在无法解析的列类型时会汇总错误并抛出异常。
    /// </remarks>
    public static void Generate(IEnumerable<TableInfo> tables, string output, string targetNamespace, string dbmsCode, string dbmsName,
        CSharpTypeMapper? typeMapper = null, ICSharpEntityTemplate? template = null)
    {
        var result = GenerateWithDiagnostics(tables, output, targetNamespace, dbmsCode, dbmsName, typeMapper, template);
        var errors = result.Diagnostics.Where(x => x.Code == "TYPE_ERROR").ToArray();
        if (errors.Length > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine,
                errors.Select(x => $"[{x.Table}.{x.Column}] {x.Message}")));
    }

    /// <summary>
    /// 生成实体文件并返回诊断结果。
    /// </summary>
    /// <param name="tables">待生成的 PDM 表。</param>
    /// <param name="output">实体文件输出目录。</param>
    /// <param name="targetNamespace">生成类型使用的命名空间。</param>
    /// <param name="dbmsCode">PDM 数据库管理系统代码。</param>
    /// <param name="dbmsName">PDM 数据库管理系统名称。</param>
    /// <param name="typeMapper">可选的类型映射器。</param>
    /// <param name="template">可选的实体模板。</param>
    /// <returns>包含生成诊断和成功写入文件数量的结果。</returns>
    /// <remarks>
    /// 类型错误会阻止文件写入；已配置的兜底映射会记录诊断并继续生成。
    /// </remarks>
    public static CSharpGenerationResult GenerateWithDiagnostics(IEnumerable<TableInfo> tables, string output,
        string targetNamespace, string dbmsCode, string dbmsName,
        CSharpTypeMapper? typeMapper = null, ICSharpEntityTemplate? template = null)
        => GenerateWithDiagnostics(tables, output, targetNamespace, dbmsCode, dbmsName, typeMapper, template, null);

    /// <summary>
    /// 生成实体并保护指定输入文件。
    /// </summary>
    internal static CSharpGenerationResult GenerateWithDiagnostics(IEnumerable<TableInfo> tables, string output,
        string targetNamespace, string dbmsCode, string dbmsName,
        CSharpTypeMapper? typeMapper, ICSharpEntityTemplate? template, string? inputPath)
    {
        typeMapper ??= new CSharpTypeMapper();
        template ??= new DefaultCSharpEntityTemplate();
        if (!Regex.IsMatch(targetNamespace, @"^[_\p{L}][\p{L}\p{Nd}_]*(\.[_\p{L}][\p{L}\p{Nd}_]*)*$") ||
            targetNamespace.Split('.').Any(Keywords.Contains))
            throw new ArgumentException("Invalid C# namespace.");
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<(string Name, string Source)>();
        var diagnostics = new List<CSharpGenerationDiagnostic>();
        foreach (var table in tables)
        {
            var name = UniqueName(Identifier(table.Code, "Entity"), usedNames);
            var usedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var properties = new List<CSharpEntityProperty>(table.Columns.Count);
            var tableHasErrors = false;
            foreach (var column in table.Columns)
            {
                var property = UniqueName(Identifier(column.Code, "Column"), usedProperties);
                if (string.Equals(property, name, StringComparison.Ordinal))
                    property = UniqueName(property + "Value", usedProperties);
                var resolution = typeMapper.Resolve(column, dbmsCode, dbmsName);
                if (resolution.TypeName == null) tableHasErrors = true;
                if (resolution.TypeName == null || resolution.UsedFallback)
                    diagnostics.Add(new CSharpGenerationDiagnostic(resolution.UsedFallback ? "TYPE_FALLBACK" : "TYPE_ERROR",
                        table.Code ?? table.Id, column.Code ?? column.Id, resolution.Reason ?? "Unknown type."));
                if (resolution.TypeName != null)
                    properties.Add(new CSharpEntityProperty(property, resolution.TypeName, column));
            }
            if (tableHasErrors) continue;
            var context = new CSharpEntityTemplateContext(targetNamespace, name, table, properties);
            var source = template.Render(context);
            if (source == null) throw new InvalidOperationException($"Entity template returned null for table '{table.Code}'.");
            files.Add((name, source));
        }
        if (diagnostics.Any(x => x.Code == "TYPE_ERROR"))
            return new CSharpGenerationResult(diagnostics, 0);
        if (inputPath != null)
            foreach (var file in files)
                Program.EnsureInputIsNotOutput(inputPath, Path.Combine(output, file.Name + ".cs"));
        using var batch = new PdmOutputBatch(output, files.Select(x => x.Name + ".cs"));
        foreach (var file in files)
            File.WriteAllText(batch.StagePath(file.Name + ".cs"), file.Source, new UTF8Encoding(false));
        batch.Commit();
        return new CSharpGenerationResult(diagnostics, files.Count);
    }

    /// <summary>
    /// 为候选名称生成未重复的名称。
    /// </summary>
    /// <param name="candidate">候选名称。</param>
    /// <param name="used">已使用的名称集合。</param>
    /// <returns>唯一名称。</returns>
    private static string UniqueName(string candidate, HashSet<string> used)
    {
        var name = candidate;
        var suffix = 2;
        while (!used.Add(name)) name = candidate + "_" + suffix++;
        return name;
    }

    /// <summary>
    /// 将数据库名称转换为合法的 C# 标识符。
    /// </summary>
    /// <param name="value">源名称。</param>
    /// <param name="fallback">源名称为空时使用的默认名称。</param>
    /// <returns>规范化后的 C# 标识符。</returns>
    private static string Identifier(string? value, string fallback)
    {
        var parts = Regex.Split(value ?? string.Empty, @"[^\p{L}\p{Nd}]+")
            .Where(x => x.Length > 0);
        var result = string.Concat(parts.Select(x => char.ToUpperInvariant(x[0]) + x.Substring(1)));
        if (result.Length == 0) result = fallback;
        if (!char.IsLetter(result[0]) && result[0] != '_') result = "_" + result;
        return Keywords.Contains(result) ? result + "_" : result;
    }

}
