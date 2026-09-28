namespace Bing.Pdm.Tool;

/// <summary>
/// 描述一次数据库列类型解析结果。
/// </summary>
public sealed class CSharpTypeResolution
{
    /// <summary>
    /// 获取解析得到的 CLR 类型名称。
    /// </summary>
    public string? TypeName { get; }
    /// <summary>
    /// 获取一个值，指示是否使用了兜底映射。
    /// </summary>
    public bool UsedFallback { get; }
    /// <summary>
    /// 获取解析失败或使用兜底映射时的原因。
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// 初始化一个 <see cref="CSharpTypeResolution"/> 类型的实例。
    /// </summary>
    /// <param name="typeName">解析得到的 CLR 类型名称。</param>
    /// <param name="usedFallback">是否使用了兜底映射。</param>
    /// <param name="reason">解析失败或使用兜底映射时的原因。</param>
    public CSharpTypeResolution(string? typeName, bool usedFallback, string? reason)
    {
        TypeName = typeName;
        UsedFallback = usedFallback;
        Reason = reason;
    }
}

/// <summary>
/// 描述一次 C# 实体生成诊断。
/// </summary>
public sealed class CSharpGenerationDiagnostic
{
    /// <summary>
    /// 获取诊断代码。
    /// </summary>
    public string Code { get; }
    /// <summary>
    /// 获取发生问题的数据表。
    /// </summary>
    public string Table { get; }
    /// <summary>
    /// 获取发生问题的数据列。
    /// </summary>
    public string Column { get; }
    /// <summary>
    /// 获取诊断消息。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 初始化一个 <see cref="CSharpGenerationDiagnostic"/> 类型的实例。
    /// </summary>
    /// <param name="code">诊断代码。</param>
    /// <param name="table">发生问题的数据表。</param>
    /// <param name="column">发生问题的数据列。</param>
    /// <param name="message">诊断消息。</param>
    public CSharpGenerationDiagnostic(string code, string table, string column, string message)
    {
        Code = code;
        Table = table;
        Column = column;
        Message = message;
    }
}

/// <summary>
/// 汇总一次 C# 实体生成的诊断和文件数量。
/// </summary>
public sealed class CSharpGenerationResult
{
    /// <summary>
    /// 获取生成过程产生的诊断。
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<CSharpGenerationDiagnostic> Diagnostics { get; }
    /// <summary>
    /// 获取成功生成的文件数量。
    /// </summary>
    public int FileCount { get; }

    /// <summary>
    /// 初始化一个 <see cref="CSharpGenerationResult"/> 类型的实例。
    /// </summary>
    /// <param name="diagnostics">生成过程产生的诊断。</param>
    /// <param name="fileCount">成功生成的文件数量。</param>
    public CSharpGenerationResult(System.Collections.Generic.IReadOnlyList<CSharpGenerationDiagnostic> diagnostics, int fileCount)
    {
        Diagnostics = diagnostics;
        FileCount = fileCount;
    }
}
