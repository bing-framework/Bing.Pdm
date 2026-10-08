using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证 JSON 离线模型与原始 PDM 生成的 Office 导出内容一致。
    /// </summary>
    public sealed class PdmJsonOfficeCompatibilityTests
    {
        /// <summary>
        /// 获取完整模型 fixture 路径。
        /// </summary>
        private static string Fixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "complete.pdm");

        /// <summary>
        /// 验证 PDM 和 JSON 输入生成的 XLSX 与 DOCX 语义 XML 一致。
        /// </summary>
        [Fact]
        public void OfficeExportsPreserveSemanticContentWhenReadFromJson()
        {
            var root = FindRepositoryRoot();
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
            var tool = Path.Combine(root, "samples", "Bing.Pdm.Tool", "bin", configuration, "net8.0", "Bing.Pdm.Tool.dll");
            Assert.True(File.Exists(tool), "CLI assembly was not built: " + tool);
            var work = Path.Combine(Path.GetTempPath(), "Bing.Pdm.JsonOfficeTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                var pdmExports = Path.Combine(work, "pdm");
                var jsonExports = Path.Combine(work, "json");
                Assert.Equal(0, RunDotnet(work, tool, "export", Fixture, pdmExports, "json,xlsx,docx", "en").ExitCode);
                var json = Path.Combine(pdmExports, "complete.json");
                Assert.True(File.Exists(json));
                Assert.Equal(0, RunDotnet(work, tool, "export", json, jsonExports, "xlsx,docx", "en").ExitCode);
                AssertSemanticZipEqual(Path.Combine(pdmExports, "complete.xlsx"), Path.Combine(jsonExports, "complete.xlsx"), "XLSX");
                AssertSemanticZipEqual(Path.Combine(pdmExports, "complete.docx"), Path.Combine(jsonExports, "complete.docx"), "DOCX");
            }
            finally
            {
                if (Directory.Exists(work)) Directory.Delete(work, true);
            }
        }

        /// <summary>
        /// 比较两个 Office 文件的语义 XML。
        /// </summary>
        private static void AssertSemanticZipEqual(string leftPath, string rightPath, string format)
        {
            var left = ReadSemanticEntries(leftPath, format);
            var right = ReadSemanticEntries(rightPath, format);
            Assert.NotEmpty(left);
            Assert.Contains(format == "XLSX" ? "xl/workbook.xml" : "word/document.xml", left.Keys);
            Assert.Equal(left.Keys.OrderBy(x => x).ToArray(), right.Keys.OrderBy(x => x).ToArray());
            foreach (var entry in left.Keys)
                Assert.True(string.Equals(left[entry], right[entry], StringComparison.Ordinal), format + " semantic entry: " + entry);
        }

        /// <summary>
        /// 读取 Office 文件中的语义 XML。
        /// </summary>
        private static Dictionary<string, string> ReadSemanticEntries(string path, string format)
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var archive = ZipFile.OpenRead(path))
            {
                foreach (var entry in archive.Entries.Where(x => IsSemanticEntry(x.FullName, format)))
                    using (var stream = entry.Open())
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        var document = XDocument.Parse(reader.ReadToEnd(), LoadOptions.PreserveWhitespace);
                        entries[entry.FullName] = document.ToString(SaveOptions.DisableFormatting);
                    }
            }
            return entries;
        }

        /// <summary>
        /// 判断 ZIP 条目是否包含文档内容。
        /// </summary>
        private static bool IsSemanticEntry(string name, string format)
        {
            if (format == "XLSX")
                return name == "xl/workbook.xml" || name == "xl/sharedStrings.xml" || name.StartsWith("xl/worksheets/", StringComparison.Ordinal);
            return name == "word/document.xml" || name == "word/styles.xml" || name == "word/numbering.xml" || name == "word/settings.xml";
        }

        /// <summary>
        /// 查找解决方案所在目录。
        /// </summary>
        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Bing.Pdm.slnx")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException("Bing.Pdm.slnx was not found.");
        }

        /// <summary>
        /// 运行 CLI 并读取进程结果。
        /// </summary>
        private static (int ExitCode, string StandardError) RunDotnet(string workingDirectory, params string[] arguments)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using (var process = Process.Start(start))
            {
                Assert.NotNull(process);
                process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return (process.ExitCode, error);
            }
        }
    }
}
