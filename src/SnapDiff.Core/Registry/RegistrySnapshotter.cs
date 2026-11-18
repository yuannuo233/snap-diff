namespace SnapDiff.Core;

/// <summary>
/// 注册表快照器：按给定范围（根键 + 排除前缀）递归枚举注册表，
/// 把每个键和键下的每个值都变成一条 <see cref="RegistryEntry"/> 记录，边扫边通过回调送出去（流式，不占大内存）。
/// 全程只读：只用 <see cref="IRegistryReader"/> 的读操作，绝不写入。
/// </summary>
public sealed class RegistrySnapshotter
{
    private readonly IRegistryReader _reader;

    public RegistrySnapshotter(IRegistryReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// 开始扫描注册表。
    /// </summary>
    /// <param name="options">拍摄范围（根键、排除清单）。</param>
    /// <param name="counts">统计计数器（跨注册表/文件两次扫描共用一份）。</param>
    /// <param name="sink">每产生一条记录就调用一次（一般是写入快照文件）。</param>
    /// <param name="progress">进度通知（可空）。</param>
    /// <param name="ct">取消令牌。</param>
    public void Capture(
        SnapshotOptions options,
        SnapshotCounts counts,
        Action<RegistryEntry> sink,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(counts);
        ArgumentNullException.ThrowIfNull(sink);

        foreach (var root in options.RegistryRoots)
        {
            ct.ThrowIfCancellationRequested();
            Walk(root, "", options.ExcludedRegistryPrefixes, counts, sink, progress, ct);
        }

        progress?.Report(new ScanProgress
        {
            Phase = ScanPhase.Registry,
            ProcessedCount = counts.RegistryKeyCount + counts.RegistryValueCount,
            SkippedCount = counts.SkippedRegistryKeyCount,
            Message = "registry done",
        });
    }

    /// <summary>深度优先遍历一棵注册表子树。</summary>
    /// <param name="rootName">根键名（如 HKEY_CURRENT_USER）。</param>
    /// <param name="relativePath">相对根键的路径，空字符串表示根键自己。</param>
    private void Walk(
        string rootName,
        string relativePath,
        List<string> excludedPrefixes,
        SnapshotCounts counts,
        Action<RegistryEntry> sink,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // 排除前缀匹配（大小写不敏感）：命中就整棵子树都不扫
        foreach (var prefix in excludedPrefixes)
        {
            if (prefix.Length > 0 && relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return;
        }

        var keyData = _reader.ReadKey(rootName, relativePath);
        if (keyData is null)
        {
            // 键不存在或没有权限：跳过并计数，不中断整个扫描
            counts.AddSkippedRegistryKeyCount();
            return;
        }

        var fullPath = relativePath.Length == 0 ? rootName : rootName + "\\" + relativePath;

        // 键本身也记一条（ValueName = null 表示“这个键存在”），这样新建的空键也能被对比发现
        sink(new RegistryEntry { KeyPath = fullPath, ValueName = null, TypeName = "", Data = "" });
        counts.AddRegistryKeyCount();

        // 每扫 250 个键报告一次进度，避免刷屏
        if (counts.RegistryKeyCount % 250 == 0)
        {
            progress?.Report(new ScanProgress
            {
                Phase = ScanPhase.Registry,
                ProcessedCount = counts.RegistryKeyCount,
                SkippedCount = counts.SkippedRegistryKeyCount,
                Message = fullPath,
            });
        }

        foreach (var value in keyData.Values)
        {
            sink(new RegistryEntry
            {
                KeyPath = fullPath,
                ValueName = value.Name,
                TypeName = RegistryValueFormatter.GetTypeName(value.Kind),
                Data = RegistryValueFormatter.FormatValue(value.Kind, value.RawValue),
            });
            counts.AddRegistryValueCount();
        }

        foreach (var subKeyName in keyData.SubKeyNames)
        {
            var childPath = relativePath.Length == 0 ? subKeyName : relativePath + "\\" + subKeyName;
            Walk(rootName, childPath, excludedPrefixes, counts, sink, progress, ct);
        }
    }
}
