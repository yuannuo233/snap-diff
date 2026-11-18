namespace SnapDiff.Core;

/// <summary>变化的种类：新增 / 删除 / 修改。</summary>
public enum ChangeKind
{
    Added,
    Removed,
    Modified,
}

/// <summary>注册表的一条变化（键或值）。</summary>
public sealed class RegistryChange
{
    /// <summary>变化的种类。</summary>
    public ChangeKind Kind { get; }

    /// <summary>变化前的样子（快照 A）。新增时为 null。</summary>
    public RegistryEntry? Before { get; }

    /// <summary>变化后的样子（快照 B）。删除时为 null。</summary>
    public RegistryEntry? After { get; }

    public RegistryChange(ChangeKind kind, RegistryEntry? before, RegistryEntry? after)
    {
        Kind = kind;
        Before = before;
        After = after;
    }

    /// <summary>取“代表这条变化”的条目（用于排序、显示）。</summary>
    public RegistryEntry Main => After ?? Before!;
}

/// <summary>文件的一条变化。</summary>
public sealed class FileChange
{
    /// <summary>变化的种类。</summary>
    public ChangeKind Kind { get; }

    /// <summary>变化前的样子（快照 A）。新增时为 null。</summary>
    public FileEntry? Before { get; }

    /// <summary>变化后的样子（快照 B）。删除时为 null。</summary>
    public FileEntry? After { get; }

    public FileChange(ChangeKind kind, FileEntry? before, FileEntry? after)
    {
        Kind = kind;
        Before = before;
        After = after;
    }

    /// <summary>取“代表这条变化”的条目（用于排序、显示）。</summary>
    public FileEntry Main => After ?? Before!;
}

/// <summary>对比结果的数量汇总。</summary>
public sealed class DiffSummary
{
    public int AddedRegistry { get; set; }
    public int RemovedRegistry { get; set; }
    public int ModifiedRegistry { get; set; }
    public int AddedFiles { get; set; }
    public int RemovedFiles { get; set; }
    public int ModifiedFiles { get; set; }

    /// <summary>变化总数。</summary>
    public int Total() => AddedRegistry + RemovedRegistry + ModifiedRegistry + AddedFiles + RemovedFiles + ModifiedFiles;
}

/// <summary>两张快照的对比结果：全部变化 + 汇总 + 两张快照的元数据。</summary>
public sealed class DiffResult
{
    public List<RegistryChange> RegistryChanges { get; } = new();
    public List<FileChange> FileChanges { get; } = new();
    public DiffSummary Summary { get; set; } = new();
    public SnapshotMetadata? SnapshotA { get; set; }
    public SnapshotMetadata? SnapshotB { get; set; }

    /// <summary>对比完成时间（UTC）。</summary>
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}
