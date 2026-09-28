using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bing.Pdm.Reader;
using SmartCode;
using SmartCode.Generator.Entity;

namespace Bing.PdmGenerateDemo
{
    /// <summary>
    /// 为 SmartCode 表生成提供 PDM 数据源。
    /// </summary>
    public class PdmDbSource : IDataSource
    {
        /// <summary>
        /// 保存待读取的 PDM 文件路径。
        /// </summary>
        private string _filePath;

        /// <inheritdoc />
        public bool Initialized { get; private set; }

        /// <inheritdoc />
        public virtual string Name { get; private set; } = "Pdm";

        /// <inheritdoc />
        public IEnumerable<Table> Tables { get; private set; } = Enumerable.Empty<Table>();

        /// <inheritdoc />
        public void Initialize(IDictionary<string, object> parameters)
        {
            if (parameters != null)
            {
                if (parameters.Value("Name", out string name) && !string.IsNullOrWhiteSpace(name))
                    Name = name;

                parameters.Value("FilePath", out _filePath);
            }

            Initialized = true;
        }

        /// <inheritdoc />
        public Task InitData()
        {
            if (!Initialized)
                throw new InvalidOperationException("Initialize the PDM data source before loading data.");
            if (string.IsNullOrWhiteSpace(_filePath))
                throw new ArgumentException("PDM data source requires the 'FilePath' initialization parameter.");

            var path = Path.GetFullPath(_filePath);
            if (!File.Exists(path))
                throw new FileNotFoundException($"PDM data source file was not found: '{path}'.", path);

            var model = new PdmReader().ReadFromFile(path);
            Tables = model.AllTables.Select((table, tableIndex) => new Table
            {
                Id = ParseInt(table.Id) ?? tableIndex + 1,
                TypeName = table.Code ?? table.Name,
                ConvertedName = table.Code ?? table.Name,
                Description = table.Comment ?? table.Description,
                Columns = table.Columns.Select((column, columnIndex) => new Column
                {
                    Id = ParseInt(column.Id) ?? columnIndex + 1,
                    Name = column.Code ?? column.Name,
                    ConvertedName = column.Code ?? column.Name,
                    DbType = column.DataType,
                    DataLength = ParseLong(column.Length),
                    Description = column.Comment ?? column.Description,
                    IsNullable = !column.Mandatory,
                    IsPrimaryKey = column.PrimaryKey,
                    AutoIncrement = column.Identity
                }).ToArray()
            }).ToArray();

            return Task.CompletedTask;
        }

        /// <summary>
        /// 尝试解析整数值。
        /// </summary>
        /// <param name="value">待解析的文本。</param>
        /// <returns>解析出的整数；解析失败时返回 <see langword="null"/>。</returns>
        private static int? ParseInt(string value) => int.TryParse(value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var result) ? (int?)result : null;

        /// <summary>
        /// 尝试解析长整数值。
        /// </summary>
        /// <param name="value">待解析的文本。</param>
        /// <returns>解析出的长整数；解析失败时返回 <see langword="null"/>。</returns>
        private static long? ParseLong(string value) => long.TryParse(value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var result) ? (long?)result : null;
    }
}
