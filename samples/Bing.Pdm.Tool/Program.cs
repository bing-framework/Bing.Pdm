using System.Text;
using System.Text.RegularExpressions;
using Bing.Pdm;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Reader;

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
        if (args.Length < 3 || (args[0] != "export" && args[0] != "generate"))
        {
            Console.Error.WriteLine("Usage: pdm export <input.pdm> <output-dir> [json,md,html,svg,xlsx,docx] [en|zh]");
            Console.Error.WriteLine("       pdm generate <input.pdm> <output-dir> [namespace] [--map <dbms|*>:<database-type>=<C#-type>]... [--fallback-type <C#-type>]");
            return 2;
        }

        try
        {
            var model = new PdmReader().ReadFromFile(args[1]);
            if (args[0] == "export")
                Export(model, args[1], args[2], args.Length > 3 ? args[3] : "json,md,html", ParseLanguage(args.Length > 4 ? args[4] : "en"));
            else
            {
                var hasNamespace = args.Length > 3 && !args[3].StartsWith("--", StringComparison.Ordinal);
                var targetNamespace = hasNamespace ? args[3] : "Generated.Entities";
                var optionIndex = hasNamespace ? 4 : 3;
                var generation = EntityGenerator.GenerateWithDiagnostics(model.AllTables, args[2], targetNamespace,
                    model.DbmsCode, model.DbmsName, ReadTypeMappings(args, optionIndex));
                foreach (var diagnostic in generation.Diagnostics)
                    Console.Error.WriteLine($"{diagnostic.Code} [{diagnostic.Table}.{diagnostic.Column}]: {diagnostic.Message}");
                if (generation.Diagnostics.Any(x => x.Code == "TYPE_ERROR")) return 1;
            }

            foreach (var diagnostic in model.Diagnostics)
                Console.Error.WriteLine($"{diagnostic.Code} [{diagnostic.SourceId}]: {diagnostic.Message}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException or ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// 按指定格式导出 PDM 模型。
    /// </summary>
    /// <param name="model">已读取的 PDM 模型。</param>
    /// <param name="input">输入文件路径。</param>
    /// <param name="output">输出目录。</param>
    /// <param name="formats">逗号分隔的导出格式。</param>
    /// <param name="language">导出内容语言。</param>
    private static void Export(Bing.Pdm.Models.PdmInfo model, string input, string output, string formats, PdmExportLanguage language)
    {
        var names = ParseExportFormats(formats);
        Directory.CreateDirectory(output);
        var exporter = new PdmExporter(language);
        var officeExporter = new PdmOfficeExporter(language);
        var basename = Path.GetFileNameWithoutExtension(input);
        var diagramPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var format in names)
        {
            if (format == "svg")
            {
                var diagrams = model.AllPhysicalDiagrams.ToArray();
                if (diagrams.Length == 0) throw new InvalidOperationException("The model has no physical diagrams to export.");
                foreach (var diagram in diagrams)
                {
                    var suffix = FileNamePart(diagram.Code ?? diagram.Name ?? diagram.Id);
                    var path = UniqueDiagramPath(output, basename, suffix, diagramPaths);
                    using var svg = new StreamWriter(path, false, new UTF8Encoding(false));
                    exporter.WriteDiagram(model, diagram, svg);
                    Console.WriteLine(path);
                }
                continue;
            }
            if (format is "xlsx" or "excel" or "docx" or "word")
            {
                var extension = format is "xlsx" or "excel" ? "xlsx" : "docx";
                var path = Path.Combine(output, basename + "." + extension);
                using var stream = File.Create(path);
                if (extension == "xlsx") officeExporter.WriteExcel(model, stream);
                else officeExporter.WriteWord(model, stream);
                Console.WriteLine(path);
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
            using var writer = new StreamWriter(textPath, false, new UTF8Encoding(false));
            exporter.Write(model, textFormat, writer);
            Console.WriteLine(textPath);
        }
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
        Directory.CreateDirectory(output);
        foreach (var file in files)
            File.WriteAllText(Path.Combine(output, file.Name + ".cs"), file.Source, new UTF8Encoding(false));
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
