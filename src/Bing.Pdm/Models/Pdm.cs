using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Pdm.Models
{
    /// <summary>
    /// PDM 结构
    /// </summary>
    public class Pdm
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
        /// 数据库编码
        /// </summary>
        public string DbCode { get; set; }

        /// <summary>
        /// 数据库名称
        /// </summary>
        public string DbName { get; set; }

        /// <summary>
        /// PDM 物理图列表
        /// </summary>
        public List<PdmPhysicalDiagram> PhysicalDiagrams { get; set; } = new List<PdmPhysicalDiagram>();

        /// <summary>
        /// PDM 用户列表
        /// </summary>
        public List<PdmUser> Users { get; set; } = new List<PdmUser>();

        /// <summary>
        /// PDM 表列表
        /// </summary>
        public List<PdmTable> Tables { get; set; } = new List<PdmTable>();

        /// <summary>
        /// PDM 引用列表
        /// </summary>
        public List<PdmReference> References { get; set; } = new List<PdmReference>();

        /// <summary>
        /// 查找用户
        /// </summary>
        /// <param name="id">用户ID</param>
        public PdmUser FindUser(string id)
        {
            var result = Users.FirstOrDefault(x => x.Id == id);
            if (result == null)
                throw new ArgumentException($"Id编号{id}，用户没有找到");
            return result;
        }

        /// <summary>
        /// 查找表
        /// </summary>
        /// <param name="id">表ID</param>
        public PdmTable FindTable(string id)
        {
            var result = Tables.FirstOrDefault(x => x.Id == id);
            if (result == null)
                throw new ArgumentException($"Id编号{id}，表没有找到");
            return result;
        }

        /// <summary>
        /// 查找引用
        /// </summary>
        /// <param name="id">引用ID</param>
        public PdmReference FindReference(string id)
        {
            var result = References.FirstOrDefault(x => x.Id == id);
            if (result == null)
                throw new ArgumentException($"Id编号{id}，引用没有找到");
            return result;
        }
    }
}
