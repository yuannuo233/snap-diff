using System.Globalization;
using System.Text;

namespace SnapDiff.Core;

/// <summary>
/// 把对比结果导出成 CSV（Excel 可直接打开）。
/// 编码：UTF-8 带 BOM（Excel 打开中文不乱码）。
/// 转义：遵循 RFC 4180 —— 含逗号、双引号、换行的字段用双引号包裹，字段内的双引号翻倍。
/// </summary>
public static class CsvExporter
{
    /// <summary>表头（15 列）。</summary>
    public const string HeaderLine =
        "变化类别,变化类型,根,路径,值名,旧类型,旧数据,新类型,新数据,旧大小(字节),新大小(字节),旧修改时间(UTC),新修改时间(UTC),旧SHA256,新SHA256";

    /// <summary>导出对比结果到 CSV 文件。</summary>
    public static void Export(DiffResult diff, string path)
    {
        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine(HeaderLine);
        foreach (var change in diff.FileChanges) WriteFileRow(writer, change);
        foreach (var change in diff.RegistryChanges) WriteRegistryRow(writer, change);
    }

    /// <summary>
    /// CSV 字段转义（RFC 4180）：包含逗号、双引号或换行的字段用双引号包起来，
    /// 字段内部的双引号写成两个双引号。
    /// 审查修复（F-15，CSV 公式注入防御）：以 =、+、-、@ 或制表符开头的字段，
    /// 在 Excel/WPS 里打开时会被当作公式执行（攻击面：恶意构造的文件名或注册表数据）。
    /// 对这类字段加一个前导单引号——Excel 会把它当纯文本显示，单引号本身不显示。
    /// </summary>
    public static string EscapeField(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "";

        // 公式注入防御：危险前缀先补单引号
        if (field[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            field = "'" + field;
        }

        bool needQuote = field.Contains(',')
                         || field.Contains('"')
                         || field.Contains('\r')
                         || field.Contains('\n');
        if (!needQuote) return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }

    private static void WriteRegistryRow(TextWriter writer, RegistryChange change)
    {
        var entry = change.Main;
        writer.WriteLine(string.Join(",",
            EscapeField(DisplayText.Registry),
            EscapeField(DisplayText.Kind(change.Kind)),
            EscapeField(entry.KeyPath),                 // “根”列放完整键路径
            EscapeField(""),                            // “路径”列对注册表行留空
            EscapeField(DisplayText.ValueName(entry)),
            EscapeField(change.Before?.TypeName ?? ""),
            EscapeField(change.Before?.Data ?? ""),
            EscapeField(change.After?.TypeName ?? ""),
            EscapeField(change.After?.Data ?? ""),
            "", "", "", "", "", ""));                   // 大小/时间/哈希列对注册表行留空
    }

    private static void WriteFileRow(TextWriter writer, FileChange change)
    {
        var entry = change.Main;
        writer.WriteLine(string.Join(",",
            EscapeField(DisplayText.Files),
            EscapeField(DisplayText.Kind(change.Kind)),
            EscapeField(entry.RootPath),                // “根”列放扫描根目录
            EscapeField(entry.RelativePath),            // “路径”列放相对路径
            EscapeField(""),                            // “值名”列对文件行留空
            "", "", "", "",                             // 类型/数据列对文件行留空
            EscapeField(change.Before?.Size.ToString(CultureInfo.InvariantCulture) ?? ""),
            EscapeField(change.After?.Size.ToString(CultureInfo.InvariantCulture) ?? ""),
            EscapeField(change.Before is null ? "" : FormatUtils.FormatUtcTime(change.Before.ModifiedTimeUtc)),
            EscapeField(change.After is null ? "" : FormatUtils.FormatUtcTime(change.After.ModifiedTimeUtc)),
            EscapeField(change.Before?.Sha256 ?? ""),
            EscapeField(change.After?.Sha256 ?? "")));
    }
}
