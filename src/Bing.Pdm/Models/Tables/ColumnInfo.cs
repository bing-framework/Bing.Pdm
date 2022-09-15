using System;

namespace Bing.Pdm.Models.Tables
{
    /// <summary>
    /// 表列信息
    /// </summary>
    public class ColumnInfo : PdmCommonInfo, IDescription
    {
        /// <summary>
        /// 数据类型
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// 数据长度
        /// </summary>
        public string Length { get; set; }

        /// <summary>
        /// 精度
        /// </summary>
        public string Precision { get; set; }

        /// <summary>
        /// 是否自增
        /// </summary>
        public bool Identity { get; set; }

        /// <summary>
        /// 是否禁止为空
        /// </summary>
        [NodeChild("Column.Mandatory")]
        public bool Mandatory { get; set; }

        /// <summary>
        /// 扩展属性文本
        /// </summary>
        public string ExtendedAttributesText { get; set; }

        /// <summary>
        /// 物理选项
        /// </summary>
        public string PhysicalOptions { get; set; }

        /// <summary>
        /// 是否主键
        /// </summary>
        public bool PrimaryKey { get; set; }

        /// <summary>
        /// 系统数据类型
        /// </summary>
        public string SystemDataType { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 获取列类型
        /// </summary>
        public string GetColumnType()
        {
            var dataType = DataType.ToUpper();

            if (dataType.Contains("CHAR")) return "string";
            if (dataType.Contains("VARCHAR")) return "string";
            if (dataType.Contains("LONGTEXT")) return "string";
            if (dataType.Contains("NUMBER")) dataType = "DECIMAL";
            else if (dataType.Contains("NUMERIC")) dataType = "DECIMAL";
            else if (dataType.Contains("DECIMAL")) dataType = "DECIMAL";

            switch (dataType)
            {
                case "INT":
                case "INTEGER":
                    return Mandatory ? "int" : "int?";
                case "TINYINT":
                    return Mandatory ? "int" : "int?";
                case "BIGINT":
                    return Mandatory ? "long" : "long?";
                case "SMALLINT":
                    return "short";
                case "DATETIME":
                case "DATE":
                    return Mandatory ? "DateTime" : "DateTime?";
                case "BOOL":
                case "BIT":
                    return Mandatory ? "bool" : "bool?";
                case "TEXT":
                    return "string";
                case "UNIQUEIDENTIFIER":
                    return Mandatory ? "Guid" : "Guid?";
                case "DECIMAL":
                    return Mandatory ? "decimal" : "decimal?";
                case "DOUBLE":
                    return Mandatory ? "double" : "double?";
            }

            throw new Exception($"数据库列{Code}类型{DataType}无法转换为System基础类型");
        }
    }
}
