namespace SnapDiff.Core;

/// <summary>
/// 对比引擎：输入两张快照，输出“新增 / 删除 / 修改”三类变化。
/// 算法：
/// 1. 把快照 A 的所有条目装进字典（键 = 条目的“身份”字符串）；
/// 2. 快照 B 流式逐条喂进来：字典里查不到 → 新增；查得到但内容变了 → 修改，并从字典移除；
/// 3. 扫完之后字典里还剩下的，就是快照 B 里已经消失的 → 删除。
/// 这样只有快照 A 需要完整在内存里，快照 B 逐条处理，尽量省内存。
/// </summary>
public static class SnapshotComparer
{
    /// <summary>内存版对比：直接给两组条目（测试和高级用法用）。</summary>
    public static DiffResult Diff(
        IEnumerable<RegistryEntry> registryA,
        IEnumerable<FileEntry> fileA,
        IEnumerable<RegistryEntry> registryB,
        IEnumerable<FileEntry> fileB,
        SnapshotMetadata? snapshotA = null,
        SnapshotMetadata? snapshotB = null,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var acc = new Accumulator(registryA, fileA);
        long processed = 0;

        foreach (var entry in registryB)
        {
            ct.ThrowIfCancellationRequested();
            acc.FeedRegistry(entry);
            ReportEvery(progress, ++processed, 2000, "registry");
        }
        foreach (var entry in fileB)
        {
            ct.ThrowIfCancellationRequested();
            acc.FeedFile(entry);
            ReportEvery(progress, ++processed, 2000, "files");
        }

        acc.Finish();
        acc.Result.SnapshotA = snapshotA;
        acc.Result.SnapshotB = snapshotB;
        return acc.Result;
    }

    /// <summary>
    /// 直接对比两个快照文件：快照 A 完整载入内存（要装字典），快照 B 流式逐行读。
    /// </summary>
    public static DiffResult DiffFiles(
        string pathA,
        string pathB,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var dataA = SnapshotReader.Load(pathA);
        var acc = new Accumulator(dataA.RegistryEntries, dataA.FileEntries);

        SnapshotMetadata? snapshotB = null;
        long processed = 0;
        foreach (var line in SnapshotReader.ReadLines(pathB))
        {
            if ((processed & 0xFFF) == 0) ct.ThrowIfCancellationRequested();
            switch (line.Kind)
            {
                case SnapshotFile.KindHeader:
                    snapshotB = line.Header;
                    break;
                case SnapshotFile.KindRegistry:
                    if (line.Registry is not null) acc.FeedRegistry(line.Registry);
                    break;
                case SnapshotFile.KindFile:
                    if (line.File is not null) acc.FeedFile(line.File);
                    break;
            }
            processed++;
            if (processed % 2000 == 0)
            {
                progress?.Report(new ScanProgress
                {
                    Phase = ScanPhase.Diffing,
                    ProcessedCount = processed,
                    Message = "diffing",
                });
            }
        }

        acc.Finish();
        acc.Result.SnapshotA = dataA.Metadata;
        acc.Result.SnapshotB = snapshotB;
        return acc.Result;
    }

    private static void ReportEvery(IProgress<ScanProgress>? progress, long processed, int every, string stage)
    {
        if (progress is not null && processed % every == 0)
        {
            progress.Report(new ScanProgress { Phase = ScanPhase.Diffing, ProcessedCount = processed, Message = stage });
        }
    }

    /// <summary>对比累积器（内部类）：承载两个字典和结果列表。</summary>
    private sealed class Accumulator
    {
        // 字典键比较用 OrdinalIgnoreCase：注册表路径和 Windows 文件路径都不区分大小写
        private readonly Dictionary<string, RegistryEntry> _registryMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, FileEntry> _fileMap = new(StringComparer.OrdinalIgnoreCase);

        public DiffResult Result { get; } = new();

        public Accumulator(IEnumerable<RegistryEntry> registryA, IEnumerable<FileEntry> fileA)
        {
            foreach (var entry in registryA) _registryMap[entry.GetIdentity()] = entry;
            foreach (var entry in fileA) _fileMap[entry.GetIdentity()] = entry;
        }

        public void FeedRegistry(RegistryEntry b)
        {
            var identity = b.GetIdentity();
            if (_registryMap.TryGetValue(identity, out var a))
            {
                _registryMap.Remove(identity);
                if (!SameRegistryValue(a, b))
                    Result.RegistryChanges.Add(new RegistryChange(ChangeKind.Modified, a, b));
            }
            else
            {
                Result.RegistryChanges.Add(new RegistryChange(ChangeKind.Added, null, b));
            }
        }

        public void FeedFile(FileEntry b)
        {
            var identity = b.GetIdentity();
            if (_fileMap.TryGetValue(identity, out var a))
            {
                _fileMap.Remove(identity);
                if (!SameFile(a, b))
                    Result.FileChanges.Add(new FileChange(ChangeKind.Modified, a, b));
            }
            else
            {
                Result.FileChanges.Add(new FileChange(ChangeKind.Added, null, b));
            }
        }

        public void Finish()
        {
            // 快照 B 处理完后，字典里剩下的都是“快照 B 里消失了”的条目 → 删除
            foreach (var left in _registryMap.Values)
                Result.RegistryChanges.Add(new RegistryChange(ChangeKind.Removed, left, null));
            foreach (var left in _fileMap.Values)
                Result.FileChanges.Add(new FileChange(ChangeKind.Removed, left, null));

            // 排序，保证输出顺序稳定（界面展示和测试断言都更好写）
            Result.RegistryChanges.Sort(CompareRegistryChanges);
            Result.FileChanges.Sort(CompareFileChanges);

            Result.Summary = new DiffSummary
            {
                AddedRegistry = Result.RegistryChanges.Count(c => c.Kind == ChangeKind.Added),
                RemovedRegistry = Result.RegistryChanges.Count(c => c.Kind == ChangeKind.Removed),
                ModifiedRegistry = Result.RegistryChanges.Count(c => c.Kind == ChangeKind.Modified),
                AddedFiles = Result.FileChanges.Count(c => c.Kind == ChangeKind.Added),
                RemovedFiles = Result.FileChanges.Count(c => c.Kind == ChangeKind.Removed),
                ModifiedFiles = Result.FileChanges.Count(c => c.Kind == ChangeKind.Modified),
            };
            Result.GeneratedAtUtc = DateTime.UtcNow;
        }

        /// <summary>注册表值算不算“变了”：类型或数据（格式化文本）有任何不同就算。</summary>
        private static bool SameRegistryValue(RegistryEntry a, RegistryEntry b) =>
            string.Equals(a.TypeName, b.TypeName, StringComparison.Ordinal)
            && string.Equals(a.Data, b.Data, StringComparison.Ordinal);

        /// <summary>
        /// 文件算不算“变了”：大小或修改时间不同就算；
        /// 若两边都有 SHA256 且不同，也算（能抓到“同大小同时间但内容不同”的罕见情况）。
        /// </summary>
        private static bool SameFile(FileEntry a, FileEntry b) =>
            a.Size == b.Size
            && a.ModifiedTimeUtc == b.ModifiedTimeUtc
            && (a.Sha256 is null || b.Sha256 is null
                || string.Equals(a.Sha256, b.Sha256, StringComparison.OrdinalIgnoreCase));

        private static int CompareRegistryChanges(RegistryChange x, RegistryChange y)
        {
            var xe = x.Main;
            var ye = y.Main;
            int byKey = string.Compare(xe.KeyPath, ye.KeyPath, StringComparison.OrdinalIgnoreCase);
            if (byKey != 0) return byKey;
            return string.Compare(xe.ValueName ?? "", ye.ValueName ?? "", StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareFileChanges(FileChange x, FileChange y)
        {
            var xe = x.Main;
            var ye = y.Main;
            int byRoot = string.Compare(xe.RootPath, ye.RootPath, StringComparison.OrdinalIgnoreCase);
            if (byRoot != 0) return byRoot;
            return string.Compare(xe.RelativePath, ye.RelativePath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
