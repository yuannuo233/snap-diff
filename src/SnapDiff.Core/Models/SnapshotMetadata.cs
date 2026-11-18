namespace SnapDiff.Core;

/// <summary>快照文件的头部元数据：格式版本、拍摄时间、机器信息和拍摄范围。</summary>
public sealed class SnapshotMetadata
{
    /// <summary>快照文件格式版本（将来格式升级时靠它兼容旧文件）。</summary>
    public int FormatVersion { get; set; } = SnapshotFile.CurrentFormatVersion;

    /// <summary>拍摄时间（UTC）。</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>机器名。</summary>
    public string MachineName { get; set; } = Environment.MachineName;

    /// <summary>当前用户名（只用来在报告里区分是哪台机器拍的，不会上传到任何地方）。</summary>
    public string? UserName { get; set; } = Environment.UserName;

    /// <summary>生成快照的程序版本。</summary>
    public string AppVersion { get; set; } = "0.1";

    /// <summary>拍摄范围配置（注册表根、排除清单、文件夹、是否算哈希）。</summary>
    public SnapshotOptions Options { get; set; } = new();
}
