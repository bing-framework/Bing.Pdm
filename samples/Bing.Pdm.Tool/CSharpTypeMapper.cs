using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Bing.Pdm.Models.Tables;

namespace Bing.Pdm.Tool;

/// <summary>
/// 将数据库列类型映射为生成实体使用的 C# 属性类型。
/// </summary>
public sealed class CSharpTypeMapper
{
    /// <summary>
    /// 获取验证 CLR 类型名称的正则表达式。
    /// </summary>
    private static readonly Regex TypeNamePattern = new(
        @"^(?:global::)?[_\p{L}][\p{L}\p{Nd}_]*(?:\.[_\p{L}][\p{L}\p{Nd}_]*)*(?:\[\])?$",
        RegexOptions.Compiled);
    /// <summary>
    /// 保存按数据库类型和 DBMS 区分的自定义映射。
    /// </summary>
    private readonly Dictionary<MappingKey, TypeMapping> _customMappings = new();
    /// <summary>
    /// 保存无法识别类型时使用的兜底映射。
    /// </summary>
    private TypeMapping? _fallback;

    /// <summary>
    /// 注册未知数据库类型的兜底 CLR 类型。
    /// </summary>
    /// <param name="clrType">有效的 C# 类型名称。</param>
    /// <param name="referenceType">是否按引用类型处理可空列。</param>
    /// <remarks>
    /// 兜底映射仅用于诊断式生成，不会隐藏解析原因。
    /// </remarks>
    public void RegisterFallback(string clrType, bool referenceType = true)
    {
        if (string.IsNullOrWhiteSpace(clrType) || !TypeNamePattern.IsMatch(clrType.Trim()))
            throw new ArgumentException("The fallback must be a valid C# type name.", nameof(clrType));
        _fallback = new TypeMapping(clrType.Trim(), referenceType);
    }

    /// <summary>
    /// 解析列类型，并在需要时返回兜底结果。
    /// </summary>
    /// <param name="column">待解析的 PDM 列。</param>
    /// <param name="dbmsCode">PDM DBMS 代码。</param>
    /// <param name="dbmsName">PDM DBMS 名称。</param>
    /// <returns>解析结果及失败原因。</returns>
    public CSharpTypeResolution Resolve(ColumnInfo column, string? dbmsCode, string? dbmsName)
    {
        try { return new CSharpTypeResolution(GetCSharpType(column, dbmsCode, dbmsName), false, null); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            if (_fallback != null)
                return new CSharpTypeResolution(Format(_fallback, column.Mandatory), true, exception.Message);
            return new CSharpTypeResolution(null, false, exception.Message);
        }
    }

    /// <summary>
    /// 注册或替换数据库类型映射。
    /// </summary>
    /// <param name="databaseType">数据库类型名称。</param>
    /// <param name="clrType">目标 CLR 类型名称。</param>
    /// <param name="dbms">可选的 DBMS 名称或代码。</param>
    /// <param name="referenceType">为 true 时，可选列使用 CLR 引用类型而不追加可空后缀。</param>
    /// <remarks>
    /// DBMS 为空时注册适用于所有厂商的映射。
    /// </remarks>
    public void RegisterMapping(string databaseType, string clrType, string? dbms = null, bool referenceType = true)
    {
        var normalizedType = Normalize(databaseType, nameof(databaseType));
        if (string.IsNullOrWhiteSpace(clrType) || !TypeNamePattern.IsMatch(clrType.Trim()))
            throw new ArgumentException("The CLR type must be a simple or fully qualified C# type name, optionally ending in [].", nameof(clrType));

        _customMappings[new MappingKey(NormalizeDbms(dbms), normalizedType)] =
            new TypeMapping(clrType.Trim(), referenceType);
    }

    /// <summary>
    /// 将 PDM 列的数据库类型转换为 C# 类型。
    /// </summary>
    /// <param name="column">待转换的 PDM 列。</param>
    /// <param name="dbmsCode">PDM DBMS 代码。</param>
    /// <param name="dbmsName">PDM DBMS 名称。</param>
    /// <returns>包含可空性处理结果的 C# 类型名称。</returns>
    public string GetCSharpType(ColumnInfo column, string? dbmsCode, string? dbmsName)
    {
        if (column == null) throw new ArgumentNullException(nameof(column));
        var declaredType = Normalize(column.DataType, nameof(column.DataType));
        var baseType = BaseType(declaredType);
        var dbms = NormalizeDbms((dbmsCode ?? string.Empty) + " " + (dbmsName ?? string.Empty));

        TypeMapping? mapping;
        if (TryGetCustom(dbms, declaredType, baseType, out mapping))
            return Format(mapping!, column.Mandatory);
        if (TryGetCustom("*", declaredType, baseType, out mapping))
            return Format(mapping!, column.Mandatory);

        var isSqlServer = dbms == "sqlserver";
        var isPostgreSql = dbms == "postgresql";
        var isMySql = dbms == "mysql";
        var isOracle = dbms == "oracle";
        string type;
        bool referenceType;

        if (isMySql && declaredType.StartsWith("tinyint(1)", StringComparison.Ordinal))
        {
            type = "bool";
            referenceType = false;
        }
        else if (isMySql && TryMapMySqlInteger(baseType, out type))
        {
            referenceType = false;
        }
        else if (isSqlServer && (baseType == "timestamp" || baseType == "rowversion"))
        {
            type = "byte[]";
            referenceType = true;
        }
        else if (isPostgreSql && baseType.StartsWith("timestamp", StringComparison.Ordinal))
        {
            type = declaredType.Contains("with time zone") || baseType == "timestamptz" ? "DateTimeOffset" : "DateTime";
            referenceType = false;
        }
        else if (isPostgreSql && (baseType.StartsWith("time with time zone", StringComparison.Ordinal) || baseType == "timetz"))
        {
            type = "DateTimeOffset";
            referenceType = false;
        }
        else if (isPostgreSql && (baseType == "time" || baseType.StartsWith("time without time zone", StringComparison.Ordinal)))
        {
            type = "TimeSpan";
            referenceType = false;
        }
        else if (isOracle && baseType.StartsWith("timestamp", StringComparison.Ordinal))
        {
            type = declaredType.Contains("with time zone") || declaredType.Contains("with local time zone")
                ? "DateTimeOffset"
                : "DateTime";
            referenceType = false;
        }
        else
        {
            try
            {
                type = MapCommonType(baseType, isPostgreSql, isOracle);
            }
            catch (InvalidOperationException)
            {
                var dbmsLabel = !string.IsNullOrWhiteSpace(dbmsName) ? dbmsName : dbmsCode ?? "unknown";
                throw new InvalidOperationException($"Unsupported database type '{column.DataType}' for DBMS '{dbmsLabel}' at column '{column.Code}'.");
            }
            referenceType = type == "string" || type == "byte[]" || type == "object";
        }

        return !column.Mandatory && !referenceType ? type + "?" : type;
    }

    /// <summary>
    /// 查找匹配的自定义类型映射。
    /// </summary>
    /// <param name="dbms">规范化的 DBMS 标识。</param>
    /// <param name="declaredType">完整数据库类型名称。</param>
    /// <param name="baseType">不含长度参数的数据库类型名称。</param>
    /// <param name="mapping">匹配的类型映射；未找到时为 <see langword="null"/>。</param>
    /// <returns>找到匹配映射时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private bool TryGetCustom(string dbms, string declaredType, string baseType, out TypeMapping? mapping)
    {
        if (dbms != "*")
        {
            if (_customMappings.TryGetValue(new MappingKey(dbms, declaredType), out mapping) ||
                _customMappings.TryGetValue(new MappingKey(dbms, baseType), out mapping))
                return true;
        }
        if (dbms == "*")
            return _customMappings.TryGetValue(new MappingKey("*", declaredType), out mapping) ||
                _customMappings.TryGetValue(new MappingKey("*", baseType), out mapping);
        mapping = null;
        return false;
    }

    /// <summary>
    /// 根据列必填状态格式化 CLR 类型。
    /// </summary>
    /// <param name="mapping">类型映射配置。</param>
    /// <param name="mandatory">列是否为必填列。</param>
    /// <returns>按列可空性格式化后的 CLR 类型名称。</returns>
    private static string Format(TypeMapping mapping, bool mandatory) =>
        !mandatory && !mapping.ReferenceType ? mapping.ClrType + "?" : mapping.ClrType;

    /// <summary>
    /// 映射常见数据库类型。
    /// </summary>
    /// <param name="databaseType">规范化的数据库类型名称。</param>
    /// <param name="isPostgreSql">是否按 PostgreSQL 类型规则处理。</param>
    /// <param name="isOracle">是否按 Oracle 类型规则处理。</param>
    /// <returns>对应的 CLR 类型名称。</returns>
    private static string MapCommonType(string databaseType, bool isPostgreSql, bool isOracle)
    {
        if (isPostgreSql)
        {
            switch (databaseType)
            {
                case "int2":
                case "smallserial":
                case "serial2": return "short";
                case "int4":
                case "serial":
                case "serial4": return "int";
                case "int8":
                case "bigserial":
                case "serial8": return "long";
                case "double precision": return "double";
                case "interval": return "TimeSpan";
            }
        }

        if (isOracle)
        {
            switch (databaseType)
            {
                case "pls_integer":
                case "binary_integer": return "int";
                case "rowid":
                case "urowid": return "string";
            }
        }

        return databaseType switch
        {
            "int" or "integer" => "int",
            "serial" or "mediumint" => "int",
            "bigserial" or "bigint" => "long",
            "smallint" => "short",
            "tinyint" => "byte",
            "year" => "short",
            "bit" or "bool" or "boolean" => "bool",
            "decimal" or "numeric" or "money" or "number" => "decimal",
            "float" => "double",
            "real" or "binary_float" => "float",
            "binary_double" => "double",
            "date" or "datetime" or "datetime2" or "timestamp" => "DateTime",
            "datetimeoffset" or "timestamptz" => "DateTimeOffset",
            "time" => "TimeSpan",
            "uniqueidentifier" or "uuid" => "Guid",
            "binary" or "varbinary" or "image" or "rowversion" or "bytea" or "raw" or "blob" or "mediumblob" or "longblob" or "bfile" => "byte[]",
            "char" or "character" or "nchar" or "varchar" or "character varying" or "nvarchar" or "varchar2" or "nvarchar2" or "text" or "ntext" or "longtext" or "mediumtext" or "clob" or "nclob" or "json" or "jsonb" or "xml" or "enum" or "set" => "string",
            "sql_variant" => "object",
            _ => throw new InvalidOperationException($"Unsupported database type '{databaseType}' for code generation.")
        };
    }

    /// <summary>
    /// 映射 MySQL 整数类型及其无符号变体。
    /// </summary>
    /// <param name="databaseType">规范化的 MySQL 类型名称。</param>
    /// <param name="clrType">映射得到的 CLR 类型名称。</param>
    /// <returns>找到支持的 MySQL 整数类型时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private static bool TryMapMySqlInteger(string databaseType, out string clrType)
    {
        var unsigned = databaseType.EndsWith(" unsigned", StringComparison.Ordinal);
        var name = unsigned ? databaseType.Substring(0, databaseType.Length - " unsigned".Length) : databaseType;
        switch (name)
        {
            case "tinyint": clrType = unsigned ? "byte" : "sbyte"; return true;
            case "smallint": clrType = unsigned ? "ushort" : "short"; return true;
            case "mediumint":
            case "int":
            case "integer": clrType = unsigned ? "uint" : "int"; return true;
            case "bigint": clrType = unsigned ? "ulong" : "long"; return true;
            default: clrType = null!; return false;
        }
    }

    /// <summary>
    /// 提取不含长度参数的数据库基础类型。
    /// </summary>
    /// <param name="declaredType">完整数据库类型名称。</param>
    /// <returns>不含长度或精度参数的基础类型名称。</returns>
    private static string BaseType(string declaredType)
    {
        var parameterStart = declaredType.IndexOf('(');
        return parameterStart < 0 ? declaredType : declaredType.Substring(0, parameterStart).Trim();
    }

    /// <summary>
    /// 规范化数据库类型名称。
    /// </summary>
    /// <param name="value">待规范化的类型名称。</param>
    /// <param name="parameterName">参数名称，用于构造异常。</param>
    /// <returns>去除首尾空白并折叠空格后的类型名称。</returns>
    private static string Normalize(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A database type is required.", parameterName);
        return Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
    }

    /// <summary>
    /// 将 DBMS 名称规范化为内部标识。
    /// </summary>
    /// <param name="value">待规范化的 DBMS 名称或代码。</param>
    /// <returns>内部使用的 DBMS 标识。</returns>
    private static string NormalizeDbms(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "*";
        var dbms = Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
        if (dbms.Contains("postgres") || dbms.Contains("pgsql")) return "postgresql";
        if (dbms.Contains("mysql") || dbms.Contains("mariadb")) return "mysql";
        if (dbms.Contains("oracle")) return "oracle";
        if (dbms.Contains("mssql") || dbms.Contains("sql server")) return "sqlserver";
        return dbms;
    }

    /// <summary>
    /// 表示自定义类型映射的查找键。
    /// </summary>
    private readonly struct MappingKey : IEquatable<MappingKey>
    {
        /// <summary>
        /// 保存数据库管理系统标识。
        /// </summary>
        private readonly string _dbms;
        /// <summary>
        /// 保存数据库类型名称。
        /// </summary>
        private readonly string _databaseType;

        /// <summary>
        /// 初始化一个 <see cref="MappingKey"/> 类型的实例。
        /// </summary>
        /// <param name="dbms">规范化的 DBMS 标识。</param>
        /// <param name="databaseType">规范化的数据库类型名称。</param>
        public MappingKey(string dbms, string databaseType)
        {
            _dbms = dbms;
            _databaseType = databaseType;
        }

        /// <inheritdoc />
        public bool Equals(MappingKey other) => _dbms == other._dbms && _databaseType == other._databaseType;
        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is MappingKey other && Equals(other);
        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(_dbms, _databaseType);
    }

    /// <summary>
    /// 表示一项类型映射配置。
    /// </summary>
    private sealed class TypeMapping
    {
        /// <summary>
        /// 获取目标 CLR 类型。
        /// </summary>
        public string ClrType { get; }
        /// <summary>
        /// 获取一个值，指示目标类型是否为引用类型。
        /// </summary>
        public bool ReferenceType { get; }

        /// <summary>
        /// 初始化一个 <see cref="TypeMapping"/> 类型的实例。
        /// </summary>
        /// <param name="clrType">目标 CLR 类型名称。</param>
        /// <param name="referenceType">目标类型是否为引用类型。</param>
        public TypeMapping(string clrType, bool referenceType)
        {
            ClrType = clrType;
            ReferenceType = referenceType;
        }
    }
}
