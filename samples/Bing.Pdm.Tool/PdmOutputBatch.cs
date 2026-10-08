using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Bing.Pdm.Tests")]

namespace Bing.Pdm.Tool;

/// <summary>
/// 暂存并发布一批输出文件。
/// </summary>
/// <remarks>处理进程内异常时回滚已发布文件；不承诺断电或进程终止时的跨文件原子性。</remarks>
internal sealed class PdmOutputBatch : IDisposable
{
    /// <summary>
    /// 保存目标目录。
    /// </summary>
    private readonly string _directory;
    /// <summary>
    /// 保存本次独立暂存目录。
    /// </summary>
    private readonly string _stage;
    /// <summary>
    /// 保存计划文件名。
    /// </summary>
    private readonly string[] _names;
    /// <summary>
    /// 保存发布前的测试回调。
    /// </summary>
    private readonly Action<int, string>? _beforePublish;
    /// <summary>
    /// 保存是否已经提交。
    /// </summary>
    private bool _committed;
    /// <summary>
    /// 保存是否需要保留恢复文件。
    /// </summary>
    private bool _retainRecovery;
    /// <summary>
    /// 保存是否已经释放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 初始化一个 <see cref="PdmOutputBatch"/> 类型的实例。
    /// </summary>
    public PdmOutputBatch(string directory, IEnumerable<string> names, Action<int, string>? beforePublish = null)
    {
        _directory = Path.GetFullPath(directory);
        _names = names.ToArray();
        var unique = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var name in _names)
        {
            if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) || name == "." || name == ".." ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !unique.Add(name))
                throw new ArgumentException("Output names must be unique file names within the output directory.", nameof(names));
            if (Directory.Exists(Path.Combine(_directory, name)))
                throw new IOException("An output file is occupied by a directory: " + name);
        }
        var parent = Path.GetDirectoryName(_directory) ?? throw new ArgumentException("Output cannot be a filesystem root.", nameof(directory));
        Directory.CreateDirectory(parent);
        _stage = Path.Combine(parent, ".pdm-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_stage);
        Directory.CreateDirectory(Path.Combine(_stage, "new"));
        Directory.CreateDirectory(Path.Combine(_stage, "old"));
        _beforePublish = beforePublish;
    }

    /// <summary>
    /// 获取计划文件的暂存路径。
    /// </summary>
    public string StagePath(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_committed || !_names.Contains(name, StringComparer.Ordinal)) throw new ArgumentException("Unknown or committed output name.", nameof(name));
        return Path.Combine(_stage, "new", name);
    }

    /// <summary>
    /// 发布全部暂存文件。
    /// </summary>
    public void Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_committed) throw new InvalidOperationException("The output batch was already committed.");
        foreach (var name in _names)
            if (!File.Exists(StagePath(name))) throw new IOException("Missing staged output: " + name);
        var createdDirectory = !Directory.Exists(_directory);
        Directory.CreateDirectory(_directory);
        var backedUp = new List<string>();
        var published = new List<string>();
        try
        {
            for (var index = 0; index < _names.Length; index++)
            {
                var name = _names[index];
                var destination = Path.Combine(_directory, name);
                _beforePublish?.Invoke(index, destination);
                if (File.Exists(destination))
                {
                    File.Move(destination, Path.Combine(_stage, "old", name));
                    backedUp.Add(name);
                }
                File.Move(StagePath(name), destination);
                published.Add(name);
            }
            _committed = true;
        }
        catch (Exception failure)
        {
            var errors = new List<Exception>();
            foreach (var name in published.AsEnumerable().Reverse())
                try { File.Delete(Path.Combine(_directory, name)); } catch (Exception error) { errors.Add(error); }
            foreach (var name in backedUp.AsEnumerable().Reverse())
                try { File.Move(Path.Combine(_stage, "old", name), Path.Combine(_directory, name)); }
                catch (Exception error) { errors.Add(error); }
            if (createdDirectory && errors.Count == 0 && !Directory.EnumerateFileSystemEntries(_directory).Any())
                try { Directory.Delete(_directory); } catch (Exception error) { errors.Add(error); }
            if (errors.Count > 0)
            {
                _retainRecovery = true;
                errors.Insert(0, failure);
                throw new IOException("Output rollback was incomplete; recovery files remain at " + _stage,
                    new AggregateException(errors));
            }
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_retainRecovery) return;
        try { Directory.Delete(_stage, true); }
        catch (IOException error) { Console.Error.WriteLine("OUTPUT_CLEANUP_WARNING: " + _stage + ": " + error.Message); }
        catch (UnauthorizedAccessException error) { Console.Error.WriteLine("OUTPUT_CLEANUP_WARNING: " + _stage + ": " + error.Message); }
    }
}
