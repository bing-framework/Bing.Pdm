using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Pdm.Models.Others;
using Bing.Pdm.Models.PhysicalDiagrams;
using Bing.Pdm.Models.References;
using Bing.Pdm.Models.Tables;
using Bing.Pdm.Models.Views;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 信息
    /// </summary>
    public class PdmInfo : PdmCommonInfo
    {
        /// <summary>
        /// PDM标识
        /// </summary>
        public string PdmId { get; set; }

        /// <summary>
        /// 包选项文本
        /// </summary>
        public string PackageOptionsText { get; set; }

        /// <summary>
        /// 实体选项文本
        /// </summary>
        public string ModelOptionsText { get; set; }

        /// <summary>
        /// 作者
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// 版本号
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// 仓储文件名
        /// </summary>
        public string RepositoryFileName { get; set; }

        /// <summary>
        /// DBMS信息
        /// </summary>
        public DbmsInfo Dbms { get; set; }

        /// <summary>
        /// 物理图集合
        /// </summary>
        public IList<PhysicalDiagramInfo> PhysicalDiagrams { get; set; } = new List<PhysicalDiagramInfo>();

        /// <summary>
        /// 包信息列表
        /// </summary>
        [ChildObject("c:Packages", typeof(PackageInfo))]
        public List<PackageInfo> PackageInfos { get; set; }

        /// <summary>
        /// 表信息列表
        /// </summary>
        [ChildObject("c:Packages", typeof(TableInfo))]
        public List<TableInfo> TableInfos { get; set; }

        /// <summary>
        /// 引用信息列表
        /// </summary>
        [ChildObject("c:References", typeof(ReferenceInfo))]
        public List<ReferenceInfo> ReferenceInfos { get; set; }

        /// <summary>
        /// 架构集合
        /// </summary>
        public IList<SchemaInfo> Schemas { get; set; } = new List<SchemaInfo>();

        /// <summary>
        /// 默认组集合
        /// </summary>
        public IList<GroupInfo> DefaultGroups { get; set; } = new List<GroupInfo>();

        /// <summary>
        /// 目标模型集合
        /// </summary>
        public IList<TargetModelInfo> TargetModels { get; set; } = new List<TargetModelInfo>();

        ///// <summary>
        ///// 表集合
        ///// </summary>
        //public IList<TableInfo> Tables => PackageInfos.SelectMany(x => x.Tables.Select(t => t)).ToList();

        /// <summary>
        /// 视图集合
        /// </summary>
        public IList<ViewInfo> Views { get; set; } = new List<ViewInfo>();

        ///// <summary>
        ///// 引用集合
        ///// </summary>
        //public IList<ReferenceInfo> References => PackageInfos.SelectMany(x => x.References.Select(t => t)).ToList();

        /// <summary>
        /// 查找架构
        /// </summary>
        /// <param name="schemaId">架构标识</param>
        public SchemaInfo FindSchema(string schemaId)
        {
            var result = Schemas.FirstOrDefault(x => x.SchemaId == schemaId);
            if (result == null)
            {
                throw new ArgumentException($"{schemaId} Schema Not Found.");
            }

            return result;
        }

        ///// <summary>
        ///// 查找表
        ///// </summary>
        ///// <param name="tableId">表标识</param>
        //public TableInfo FindTable(string tableId)
        //{
        //    var result = Tables.FirstOrDefault(x => x.TableId == tableId);
        //    if (result == null)
        //    {
        //        throw new ArgumentException($"{tableId} Table Not Found.");
        //    }

        //    return result;
        //}

        ///// <summary>
        ///// 查找引用
        ///// </summary>
        ///// <param name="referenceId">引用标识</param>
        //public ReferenceInfo FindReference(string referenceId)
        //{
        //    var result = References.FirstOrDefault(x => x.ReferenceId == referenceId);
        //    if (result == null)
        //    {
        //        throw new ArgumentException($"{referenceId} Reference Not Found.");
        //    }

        //    return result;
        //}
    }
}
