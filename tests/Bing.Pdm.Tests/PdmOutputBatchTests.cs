using System;
using System.IO;
using System.Linq;
using Bing.Pdm.Tool;
using Xunit;

namespace Bing.Pdm.Tests
{
    /// <summary>
    /// 验证批量输出的暂存、提交、回滚和清理行为。
    /// </summary>
    public sealed class PdmOutputBatchTests
    {
        /// <summary>
        /// 验证所有暂存文件成功发布并保留无关文件。
        /// </summary>
        [Fact]
        public void CommitReplacesAllTargetsAndPreservesSentinel()
        {
            var directory = CreateDirectory();
            try
            {
                File.WriteAllText(Path.Combine(directory, "a.txt"), "old-a");
                File.WriteAllText(Path.Combine(directory, "b.txt"), "old-b");
                File.WriteAllText(Path.Combine(directory, "sentinel.txt"), "sentinel");
                using (var batch = new PdmOutputBatch(directory, new[] { "a.txt", "b.txt" }))
                {
                    File.WriteAllText(batch.StagePath("a.txt"), "new-a");
                    File.WriteAllText(batch.StagePath("b.txt"), "new-b");
                    batch.Commit();
                }

                Assert.Equal("new-a", File.ReadAllText(Path.Combine(directory, "a.txt")));
                Assert.Equal("new-b", File.ReadAllText(Path.Combine(directory, "b.txt")));
                Assert.Equal("sentinel", File.ReadAllText(Path.Combine(directory, "sentinel.txt")));
            }
            finally { DeleteDirectory(directory); }
        }

        /// <summary>
        /// 验证第二个文件发布失败时已发布文件和旧内容均回滚。
        /// </summary>
        [Fact]
        public void CommitRollsBackWhenPublishCallbackFails()
        {
            var directory = CreateDirectory();
            try
            {
                File.WriteAllText(Path.Combine(directory, "a.txt"), "old-a");
                File.WriteAllText(Path.Combine(directory, "b.txt"), "old-b");
                string stage;
                using (var batch = new PdmOutputBatch(directory, new[] { "a.txt", "b.txt" },
                    (index, _) => { if (index == 1) throw new IOException("publish failed"); }))
                {
                    File.WriteAllText(batch.StagePath("a.txt"), "new-a");
                    File.WriteAllText(batch.StagePath("b.txt"), "new-b");
                    stage = Directory.GetParent(Path.GetDirectoryName(batch.StagePath("a.txt"))).FullName;
                    Assert.Throws<IOException>(() => batch.Commit());
                }

                Assert.Equal("old-a", File.ReadAllText(Path.Combine(directory, "a.txt")));
                Assert.Equal("old-b", File.ReadAllText(Path.Combine(directory, "b.txt")));
                Assert.False(Directory.Exists(stage));
            }
            finally { DeleteDirectory(directory); }
        }

        /// <summary>
        /// 验证写入暂存失败后 Dispose 只清理本批暂存目录。
        /// </summary>
        [Fact]
        public void DisposeAfterStageFailurePreservesExistingTargets()
        {
            var directory = CreateDirectory();
            try
            {
                File.WriteAllText(Path.Combine(directory, "a.txt"), "old-a");
                string stage;
                using (var batch = new PdmOutputBatch(directory, new[] { "a.txt" }))
                {
                    File.WriteAllText(batch.StagePath("a.txt"), "new-a");
                    stage = Directory.GetParent(Path.GetDirectoryName(batch.StagePath("a.txt"))).FullName;
                    Assert.Throws<ArgumentException>(() => batch.StagePath("../escape.txt"));
                }

                Assert.Equal("old-a", File.ReadAllText(Path.Combine(directory, "a.txt")));
                Assert.False(Directory.Exists(stage));
            }
            finally { DeleteDirectory(directory); }
        }

        /// <summary>
        /// 验证输出目录创建或初始化失败时不遗留新目录。
        /// </summary>
        [Fact]
        public void FailedNewOutputDoesNotLeaveOutputDirectory()
        {
            var parent = CreateDirectory();
            var output = Path.Combine(parent, "new-output");
            try
            {
                using (var batch = new PdmOutputBatch(output, new[] { "first.json", "second.json" },
                    (index, _) => { if (index == 1) throw new IOException("publish failed"); }))
                {
                    File.WriteAllText(batch.StagePath("first.json"), "first");
                    File.WriteAllText(batch.StagePath("second.json"), "second");
                    Assert.Throws<IOException>(() => batch.Commit());
                }
                Assert.False(Directory.Exists(output));
            }
            finally { DeleteDirectory(parent); }
        }

        /// <summary>
        /// 验证输出名称不能逃逸目录且不能重复。
        /// </summary>
        [Fact]
        public void RejectsEscapingAndDuplicateNames()
        {
            var directory = CreateDirectory();
            try
            {
                Assert.Throws<ArgumentException>(() => new PdmOutputBatch(directory,
                    new[] { "../outside.txt" }));
                Assert.Throws<ArgumentException>(() => new PdmOutputBatch(directory,
                    new[] { "a.txt", "a.txt" }));
                if (OperatingSystem.IsWindows()) Assert.Throws<ArgumentException>(() => new PdmOutputBatch(directory,
                    new[] { "a.txt", "A.txt" }));
            }
            finally { DeleteDirectory(directory); }
        }

        /// <summary>
        /// 创建测试输出目录。
        /// </summary>
        private static string CreateDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "pdm-output-batch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// 删除测试输出目录。
        /// </summary>
        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
