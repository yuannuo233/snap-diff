using System.Globalization;
using System.Text;
using Microsoft.Win32;

namespace SnapDiff.Core;

/// <summary>
/// 把注册表值的数据转成“人能看懂、能存进 JSON”的字符串。
/// 例：REG_SZ 原样保留、REG_DWORD 显示无符号数字、REG_BINARY 只显示前 32 字节的十六进制。
/// </summary>
public static class RegistryValueFormatter
{
    /// <summary>REG_BINARY 数据最多显示多少个字节（超出部分只记总数，避免快照膨胀）。</summary>
    public const int MaxBinaryDisplayBytes = 32;

    /// <summary>值类型的标准名称（和注册表编辑器里的叫法一致）。</summary>
    public static string GetTypeName(RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.String => "REG_SZ",
        RegistryValueKind.ExpandString => "REG_EXPAND_SZ",
        RegistryValueKind.Binary => "REG_BINARY",
        RegistryValueKind.DWord => "REG_DWORD",
        RegistryValueKind.QWord => "REG_QWORD",
        RegistryValueKind.MultiString => "REG_MULTI_SZ",
        RegistryValueKind.None => "REG_NONE",
        RegistryValueKind.Unknown => "REG_UNKNOWN",
        _ => "REG_" + kind.ToString(),
    };

    /// <summary>把原始值格式化成可打印文本。</summary>
    public static string FormatValue(RegistryValueKind kind, object? raw)
    {
        if (raw is null) return "";
        switch (kind)
        {
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString:
                return raw as string ?? raw.ToString() ?? "";

            case RegistryValueKind.DWord:
                // DWORD 是 32 位无符号数；.NET 读出来是 int，先转成无符号再显示
                //（例如 -1 应显示为 4294967295，而不是 -1）
                return raw switch
                {
                    int i => unchecked((uint)i).ToString(CultureInfo.InvariantCulture),
                    uint u => u.ToString(CultureInfo.InvariantCulture),
                    long l => l.ToString(CultureInfo.InvariantCulture),
                    _ => raw.ToString() ?? "",
                };

            case RegistryValueKind.QWord:
                return raw switch
                {
                    long l => l.ToString(CultureInfo.InvariantCulture),
                    int i => i.ToString(CultureInfo.InvariantCulture),
                    _ => raw.ToString() ?? "",
                };

            case RegistryValueKind.MultiString:
                if (raw is string[] list) return string.Join(" | ", list);
                return raw.ToString() ?? "";

            case RegistryValueKind.Binary:
                if (raw is byte[] bytes) return FormatBinary(bytes);
                return raw.ToString() ?? "";

            case RegistryValueKind.None:
                return "";

            default:
                // 未知类型：尽力显示
                return raw.ToString() ?? "";
        }
    }

    /// <summary>
    /// 二进制数据转十六进制文本（大写、空格分隔），超过 <see cref="MaxBinaryDisplayBytes"/> 字节时
    /// 截断并追加“…(共 N 字节)”提示。
    /// 注意：截断只影响“显示”，对比时超长二进制只有前 32 字节参与逐字比较——这是已知的取舍。
    /// </summary>
    public static string FormatBinary(byte[] bytes)
    {
        if (bytes.Length == 0) return "";
        int shown = Math.Min(bytes.Length, MaxBinaryDisplayBytes);
        var sb = new StringBuilder();
        for (int i = 0; i < shown; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
        }
        if (bytes.Length > shown) sb.Append(" …(共 ").Append(bytes.Length).Append(" 字节)");
        return sb.ToString();
    }
}
