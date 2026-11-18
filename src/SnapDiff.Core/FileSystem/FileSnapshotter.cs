using System.Collections.Concurrent;

namespace SnapDiff.Core;

/// <summary>
/// 文件系统快照器：扫描用户选定的目录列表，记录每个文件的大小、修改时间（UTC）和可选的 SHA256。
/// 特点：
/// 1. 两步走——先收集目录列表（快、不读文件内容），再分块交给多个线程并行扫文件；
/// 2. 无权限的目录自动跳过并计数，绝不中断整个扫描；
/// 3. junction / 符号链接不深入（防止 AppData 里的 "Application Data" 死循环）；
/// 4. 边扫边通过回调送出记录（流式，不占大内存）。
/// 全程只读。
/// </summary>
public sealed class FileSnapshotter
{
    /// <summary>并行扫描时每个目录块的大小（把目录列表切块分给多个线程）。</summary>
    private const int ParallelRangeSize = 128;

    /// <inheritdoc cref="Capture" />
    public void Capture(
        SnapshotOptions options,
        SnapshotCounts counts,
        Action<FileEntry> sink,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(counts);
        ArgumentNullException.ThrowIfNull(sink);

        foreach (var rawRoot in options.FileSystemRoots)
        {
            ct.ThrowIfCancellationRequested();

            var root = NormalizeRoot(rawRoot);
            if (root is null)
            {
                // 目录不存在：跳过这个根，并通知界面
                progress?.Report(new ScanProgress
                {
                    Phase = ScanPhase.FileSystem,
                    SkippedCount = counts.TotalSkipped(),
                    Message = "skip missing root: " + rawRoot,
                });
                continue;
            }

            // 第一步：收集目录树（无权限目录跳过并计数；junction 不深入）
            var dirs = new List<string>();
            var unreadable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectDirectories(root, dirs, unreadable, counts, ct);

            // 第二步：目录切块，交给线程池并行扫描文件
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            };
            Parallel.ForEach(
                Partitioner.Create(0, dirs.Count, ParallelRangeSize),
                parallelOptions,
                range =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        ScanOneDirectory(dirs[i], root, unreadable, options, counts, sink, progress);
                    }
                });

            progress?.Report(new ScanProgress
            {
                Phase = ScanPhase.FileSystem,
                ProcessedCount = counts.FileCount,
                SkippedCount = counts.TotalSkipped(),
                Message = "root done: " + root,
            });
        }
    }

    /// <summary>用栈代替递归，把一个根目录下的所有子目录收集成列表。</summary>
    private static void CollectDirectories(
        string root,
        List<string> dirs,
        HashSet<string> unreadable,
        SnapshotCounts counts,
        CancellationToken ct)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();
            dirs.Add(dir);

            string[] children;
            try
            {
                children = Directory.GetDirectories(dir);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
            {
                // 进不去的目录：计一次“跳过”，标记为不可读（后面不再重复计数）
                unreadable.Add(dir);
                counts.AddSkippedDirectoryCount();
                continue;
            }

            foreach (var child in children)
            {
                try
                {
                    var attributes = File.GetAttributes(child);
                    // junction / 符号链接不深入，防止循环（例如 AppData 里的 "Application Data"）
                    if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
                {
                    counts.AddSkippedDirectoryCount();
                    continue;
                }
                stack.Push(child);
            }
        }
    }

    /// <summary>扫描单个目录里的所有文件，生成快照记录。</summary>
    private void ScanOneDirectory(
        string dir,
        string root,
        HashSet<string> unreadable,
        SnapshotOptions options,
        SnapshotCounts counts,
        Action<FileEntry> sink,
        IProgress<ScanProgress>? progress)
    {
        if (unreadable.Contains(dir)) return; // 收集阶段就知道进不去的目录，直接跳过

        string[] files;
        try
        {
            files = Directory.EnumerateFiles(dir).ToArray();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
        {
            counts.AddSkippedDirectoryCount();
            return;
        }

        foreach (var filePath in files)
        {
            try
            {
                var info = new FileInfo(filePath);
                if (!info.Exists) continue; // 扫描过程中文件消失了（被删/被改名），忽略

                string? sha = null;
                if (options.ComputeSha256 && info.Length < SnapshotOptions.MaxHashFileSizeBytes)
                    sha = FileHasher.ComputeSha256(filePath);

                sink(new FileEntry
                {
                    RootPath = root,
                    RelativePath = Path.GetRelativePath(root, filePath),
                    Size = info.Length,
                    ModifiedTimeUtc = info.LastWriteTimeUtc,
                    Sha256 = sha,
                });
                counts.AddFileCount();

                // 每扫 500 个文件报告一次进度
                if (counts.FileCount % 500 == 0)
                {
                    progress?.Report(new ScanProgress
                    {
                        Phase = ScanPhase.FileSystem,
                        ProcessedCount = counts.FileCount,
                        SkippedCount = counts.TotalSkipped(),
                        Message = dir,
                    });
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
            {
                // 单个文件读不了（被占用/权限）：跳过并计数，不影响其他文件
                counts.AddSkippedFileCount();
            }
        }
    }

    /// <summary>整理根目录路径：补全、去尾部分隔符；目录不存在返回 null。</summary>
    private static string? NormalizeRoot(string raw)
    {
        try
        {
            var trimmed = raw.Trim().TrimEnd('\\', '/');
            if (trimmed.Length == 2 && trimmed[1] == ':') trimmed += "\\"; // "C:" → "C:\"（否则 GetFullPath 语义会变）
            if (trimmed.Length == 0) return null;
            var full = Path.GetFullPath(trimmed);
            return Directory.Exists(full) ? full : null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or System.Security.SecurityException)
        {
            return null;
        }
    }
}
