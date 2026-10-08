using System;
using System.IO;
using Bing.Pdm.Models;
using Newtonsoft.Json;

namespace Bing.Pdm.Reader
{
    /// <summary>
    /// 读取本项目导出的 PDM JSON 模型。
    /// </summary>
    public sealed class PdmJsonReader : IPdmReader
    {
        /// <inheritdoc />
        public PdmInfo ReadFromFile(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
                return Read(stream);
        }

        /// <inheritdoc />
        public PdmInfo Read(Stream stream)
        {
            var root = PdmJsonMigrator.LoadCurrent(stream);
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            });
            var model = root.ToObject<PdmInfo>(serializer);
            if (model == null) throw new InvalidDataException("The JSON document is not a PDM model export.");
            model.RebuildLookup();
            return model;
        }
    }
}
