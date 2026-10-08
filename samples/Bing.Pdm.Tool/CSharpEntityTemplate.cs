using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.Pdm.Models.Tables;

namespace Bing.Pdm.Tool;

/// <summary>
/// 定义 C# 实体源代码模板。
/// </summary>
public interface ICSharpEntityTemplate
{
    /// <summary>
    /// 渲染实体源代码。
    /// </summary>
    /// <param name="context">实体模板上下文。</param>
    /// <returns>渲染得到的 C# 实体源代码。</returns>
    string Render(CSharpEntityTemplateContext context);
}

/// <summary>
/// 描述一次 C# 实体渲染所需的上下文。
/// </summary>
public sealed class CSharpEntityTemplateContext
{
    /// <summary>
    /// 获取目标命名空间。
    /// </summary>
    public string Namespace { get; }
    /// <summary>
    /// 获取实体类型名称。
    /// </summary>
    public string EntityName { get; }
    /// <summary>
    /// 获取源 PDM 表。
    /// </summary>
    public TableInfo Table { get; }
    /// <summary>
    /// 获取已解析的实体属性。
    /// </summary>
    public IReadOnlyList<CSharpEntityProperty> Properties { get; }

    /// <summary>
    /// 初始化一个 <see cref="CSharpEntityTemplateContext"/> 类型的实例。
    /// </summary>
    /// <param name="targetNamespace">目标命名空间。</param>
    /// <param name="entityName">实体类型名称。</param>
    /// <param name="table">源 PDM 表。</param>
    /// <param name="properties">已解析的实体属性。</param>
    /// <exception cref="ArgumentNullException">任一参数为 <see langword="null"/>。</exception>
    /// <remarks>
    /// 所有参数均不能为空；属性集合会复制为只读列表。
    /// </remarks>
    public CSharpEntityTemplateContext(string targetNamespace, string entityName, TableInfo table,
        IEnumerable<CSharpEntityProperty> properties)
    {
        Namespace = targetNamespace ?? throw new ArgumentNullException(nameof(targetNamespace));
        EntityName = entityName ?? throw new ArgumentNullException(nameof(entityName));
        Table = table ?? throw new ArgumentNullException(nameof(table));
        Properties = Array.AsReadOnly((properties ?? throw new ArgumentNullException(nameof(properties))).ToArray());
    }
}

/// <summary>
/// 描述一个生成的 C# 实体属性。
/// </summary>
public sealed class CSharpEntityProperty
{
    /// <summary>
    /// 获取生成的属性名称。
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// 获取生成的 CLR 类型名称。
    /// </summary>
    public string ClrType { get; }
    /// <summary>
    /// 获取源 PDM 列。
    /// </summary>
    public ColumnInfo Column { get; }

    /// <summary>
    /// 初始化一个 <see cref="CSharpEntityProperty"/> 类型的实例。
    /// </summary>
    /// <param name="name">生成的属性名称。</param>
    /// <param name="clrType">生成的 CLR 类型名称。</param>
    /// <param name="column">源 PDM 列。</param>
    /// <exception cref="ArgumentNullException">任一参数为 <see langword="null"/>。</exception>
    public CSharpEntityProperty(string name, string clrType, ColumnInfo column)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
        Column = column ?? throw new ArgumentNullException(nameof(column));
    }
}

/// <summary>
/// 使用默认布局生成 C# 实体源代码。
/// </summary>
public sealed class DefaultCSharpEntityTemplate : ICSharpEntityTemplate
{
    /// <summary>
    /// 保存可能与实体名称冲突的 CLR 系统类型名称。
    /// </summary>
    private static readonly HashSet<string> FrameworkTypes = new(StringComparer.Ordinal)
    {
        "DateTime", "DateTimeOffset", "DateOnly", "TimeOnly", "TimeSpan", "Guid"
    };

    /// <inheritdoc />
    public string Render(CSharpEntityTemplateContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        var source = new StringBuilder();
        source.AppendLine("// Generated from PDM. Review database-specific types before use.");
        source.AppendLine("using System;");
        source.AppendLine();
        source.AppendLine($"namespace {context.Namespace};");
        source.AppendLine();
        source.AppendLine($"public class {context.EntityName}");
        source.AppendLine("{");
        foreach (var property in context.Properties)
            source.AppendLine($"    public {SourceType(property.ClrType)} {property.Name} {{ get; set; }}");
        source.AppendLine("}");
        return source.ToString();
    }

    /// <summary>
    /// 限定可能被生成实体遮蔽的系统类型。
    /// </summary>
    /// <param name="type">已解析的 CLR 类型名称。</param>
    /// <returns>适合写入实体属性声明的类型名称。</returns>
    private static string SourceType(string type)
    {
        if (type.StartsWith("System.", StringComparison.Ordinal)) return "global::" + type;
        var suffix = type.EndsWith("[]", StringComparison.Ordinal) ? "[]"
            : type.EndsWith("?", StringComparison.Ordinal) ? "?" : string.Empty;
        var name = type.Substring(0, type.Length - suffix.Length);
        return FrameworkTypes.Contains(name) ? "global::System." + name + suffix : type;
    }
}
