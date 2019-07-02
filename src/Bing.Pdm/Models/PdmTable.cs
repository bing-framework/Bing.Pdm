using System.Collections.Generic;
using Bing.Pdm.Models.Keys;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 表结构
    /// </summary>
    public class PdmTable
    {
        /// <summary>
        /// 系统编号
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 注释
        /// </summary>
        public string Comment { get; set; }

        ///// <summary>
        ///// PDM 列结构列表
        ///// </summary>
        //public List<PdmColumn> Columns { get; set; } = new List<PdmColumn>();

        /// <summary>
        /// PDM 约束键列表
        /// </summary>
        public List<KeyInfo> Keys { get; set; } = new List<KeyInfo>();

        /// <summary>
        /// 主键
        /// </summary>
        public KeyInfo PrimaryKey { get; set; }

        /// <summary>
        /// PDM 索引列表
        /// </summary>
        public List<PdmIndex> Indexs { get; set; } = new List<PdmIndex>();

        /// <summary>
        /// PDM 用户
        /// </summary>
        public PdmUser User { get; set; }

        ///// <summary>
        ///// 添加列
        ///// </summary>
        ///// <param name="column">列</param>
        //public void AddColumn(PdmColumn column)
        //{
        //    column.Table = this;
        //    Columns.Add(column);
        //}

        ///// <summary>
        ///// 查找列
        ///// </summary>
        ///// <param name="id">列ID</param>
        //public PdmColumn FindColumn(string id)
        //{
        //    var result = Columns.FirstOrDefault(x => x.Id == id);
        //    if (result == null)
        //        throw new ArgumentException($"Id编号{id}，列没有找到");
        //    return result;
        //}

        ///// <summary>
        ///// 查找约束键
        ///// </summary>
        ///// <param name="id">约束ID</param>
        //public PdmKey FindKey(string id)
        //{
        //    var result = Keys.FirstOrDefault(x => x.Id == id);
        //    if (result == null)
        //        throw new ArgumentException($"Id编号{id}，约束没有找到");
        //    return result;
        //}

        /// <summary>
        /// 输出字符串
        /// </summary>
        public override string ToString()
        {
            return $"{Code}({Name})";
        }
    }
}
