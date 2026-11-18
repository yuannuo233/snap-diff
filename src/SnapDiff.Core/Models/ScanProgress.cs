namespace SnapDiff.Core;

/// <summary>扫描 / 对比进行到哪个阶段。</summary>
public enum ScanPhase
{
    NotStarted,
    Registry,
    FileSystem,
    Saving,
    Diffing,
    Completed,
}

/// <summary>扫描进度的通知数据（界面用它刷新进度文字；总数事先未知，所以不显示百分比）。</summary>
public sealed class ScanProgress
{
    /// <summary>当前阶段。</summary>
    public ScanPhase Phase { get; init; }

    /// <summary>已处理条目数（键 / 值 / 文件，视阶段而定）。</summary>
    public long ProcessedCount { get; init; }

    /// <summary>因权限不足等原因跳过的数量。</summary>
    public long SkippedCount { get; init; }

    /// <summary>一句提示（通常是当前正在处理的路径）。</summary>
    public string? Message { get; init; }
}
