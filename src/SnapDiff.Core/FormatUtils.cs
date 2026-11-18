using System.Globalization;

namespace SnapDiff.Core;

/// <summary>通用的显示格式化工具（时间、大小），界面和报告导出共用。</summary>
public static class FormatUtils
{
    /// <summary>把 UTC 时间格式化成 "2026-01-01 08:30:05 (UTC)" 这样好读、可排序的形式。</summary>
    public static string FormatUtcTime(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        return utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " (UTC)";
    }

    /// <summary>把字节数格式化成人能看懂的大小（B / KB / MB / GB…）。</summary>
    public static string HumanSize(long bytes)
    {
        if (bytes < 1024) return bytes.ToString("0", CultureInfo.InvariantCulture) + " B";
        double size = bytes;
        foreach (var unit in new[] { "KB", "MB", "GB", "TB" })
        {
            size /= 1024;
            if (size < 1024) return size.ToString("0.#", CultureInfo.InvariantCulture) + " " + unit;
        }
        return (size / 1024).ToString("0.#", CultureInfo.InvariantCulture) + " PB";
    }
}
