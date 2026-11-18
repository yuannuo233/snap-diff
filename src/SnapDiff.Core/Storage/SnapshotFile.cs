namespace SnapDiff.Core;

/// <summary>
/// 快照文件格式的常量。
/// 格式：一个 .snapdiff 文件 = GZip 压缩的 JSON Lines（UTF-8 文本）。
/// 第一行是 header（元数据），中间每行一条记录（注册表键/值 或 文件），最后一行是 footer（统计数字）。
/// </summary>
public static class SnapshotFile
{
    /// <summary>当前格式版本号。</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>每行 Kind 字段的取值。</summary>
    public const string KindHeader = "header";
    public const string KindFooter = "footer";
    public const string KindRegistry = "registry";
    public const string KindFile = "file";

    /// <summary>推荐的文件扩展名。</summary>
    public const string FileExtension = ".snapdiff";
}

/// <summary>
/// 快照文件里的一行。一行只放一种内容，用 Kind 区分——
/// 这样写入是流式的（扫到一条写一条，不需要先在内存里攒一个大列表）。
/// </summary>
public sealed class SnapshotLine
{
    /// <summary>这一行的类型：header / footer / registry / file。</summary>
    public string Kind { get; set; } = "";

    public SnapshotMetadata? Header { get; set; }
    public SnapshotCounts? Footer { get; set; }
    public RegistryEntry? Registry { get; set; }
    public FileEntry? File { get; set; }
}

/// <summary>完整载入内存的一张快照（对比时用于“快照 A”；“快照 B”用流式读取，省内存）。</summary>
public sealed class SnapshotData
{
    public SnapshotMetadata Metadata { get; set; } = new();
    public List<RegistryEntry> RegistryEntries { get; } = new();
    public List<FileEntry> FileEntries { get; } = new();
    public SnapshotCounts? Counts { get; set; }
}
