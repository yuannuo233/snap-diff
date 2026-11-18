namespace SnapDiff.Core;

/// <summary>
/// 一次快照的统计数字，最后写进快照文件末尾的 footer 行。
/// 计数用 Interlocked 原子操作——因为文件扫描是多线程并行的。
/// </summary>
public sealed class SnapshotCounts
{
    private long _registryKeyCount;
    private long _registryValueCount;
    private long _fileCount;
    private long _skippedRegistryKeyCount;
    private long _skippedDirectoryCount;
    private long _skippedFileCount;

    /// <summary>拍到的注册表键个数（“键本身”记录的条数）。</summary>
    public long RegistryKeyCount { get => _registryKeyCount; set => _registryKeyCount = value; }

    /// <summary>拍到的注册表值个数。</summary>
    public long RegistryValueCount { get => _registryValueCount; set => _registryValueCount = value; }

    /// <summary>拍到的文件个数。</summary>
    public long FileCount { get => _fileCount; set => _fileCount = value; }

    /// <summary>因权限不足等原因跳过的注册表键个数。</summary>
    public long SkippedRegistryKeyCount { get => _skippedRegistryKeyCount; set => _skippedRegistryKeyCount = value; }

    /// <summary>因权限不足等原因跳过的目录个数。</summary>
    public long SkippedDirectoryCount { get => _skippedDirectoryCount; set => _skippedDirectoryCount = value; }

    /// <summary>枚举过程中消失或读不了的文件个数。</summary>
    public long SkippedFileCount { get => _skippedFileCount; set => _skippedFileCount = value; }

    public void AddRegistryKeyCount(long delta = 1) => Interlocked.Add(ref _registryKeyCount, delta);

    public void AddRegistryValueCount(long delta = 1) => Interlocked.Add(ref _registryValueCount, delta);

    public void AddFileCount(long delta = 1) => Interlocked.Add(ref _fileCount, delta);

    public void AddSkippedRegistryKeyCount(long delta = 1) => Interlocked.Add(ref _skippedRegistryKeyCount, delta);

    public void AddSkippedDirectoryCount(long delta = 1) => Interlocked.Add(ref _skippedDirectoryCount, delta);

    public void AddSkippedFileCount(long delta = 1) => Interlocked.Add(ref _skippedFileCount, delta);

    /// <summary>拍到的条目总数（键 + 值 + 文件）。</summary>
    public long TotalEntries() => _registryKeyCount + _registryValueCount + _fileCount;

    /// <summary>跳过的总数。</summary>
    public long TotalSkipped() => _skippedRegistryKeyCount + _skippedDirectoryCount + _skippedFileCount;
}
