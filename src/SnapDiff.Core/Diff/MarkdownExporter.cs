using System.Text;

namespace SnapDiff.Core;

/// <summary>
/// 把对比结果导出成 Markdown 报告（含摘要表和六个小节：文件/注册表 × 新增/删除/修改）。
/// 报告文案为中文，方便直接贴进笔记、工单或 GitHub Issue。
/// </summary>
public static class MarkdownExporter
{
    /// <summary>
    /// Markdown 表格单元格转义：竖线会切断表格列，转义成 \|;
    /// 换行换成 &lt;br&gt;，避免表格断行。
    /// </summary>
    public static string EscapeCell(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("|", "\\|")
                   .Replace("\r\n", "<br>")
                   .Replace("\r", " ")
                   .Replace("\n", "<br>");
    }

    /// <summary>导出对比结果到 Markdown 文件。</summary>
    public static void Export(DiffResult diff, string path)
    {
        var summary = diff.Summary;
        var sb = new StringBuilder();

        sb.AppendLine("# 快照侦探（SnapDiff）对比报告");
        sb.AppendLine();
        sb.AppendLine("> 由 SnapDiff 自动生成：两次只读快照之间的差异。SnapDiff 不修改系统、不上传数据。");
        sb.AppendLine();
        if (diff.SnapshotA is not null)
            sb.AppendLine($"- 快照 A：{FormatUtils.FormatUtcTime(diff.SnapshotA.CreatedAtUtc)}（机器：{diff.SnapshotA.MachineName}）");
        if (diff.SnapshotB is not null)
            sb.AppendLine($"- 快照 B：{FormatUtils.FormatUtcTime(diff.SnapshotB.CreatedAtUtc)}（机器：{diff.SnapshotB.MachineName}）");
        sb.AppendLine($"- 生成时间：{FormatUtils.FormatUtcTime(diff.GeneratedAtUtc)}");
        sb.AppendLine($"- 变化总数：新增 {summary.AddedRegistry + summary.AddedFiles}，删除 {summary.RemovedRegistry + summary.RemovedFiles}，修改 {summary.ModifiedRegistry + summary.ModifiedFiles}");

        sb.AppendLine();
        sb.AppendLine("## 摘要");
        sb.AppendLine();
        sb.AppendLine("| 类别 | 新增 | 删除 | 修改 |");
        sb.AppendLine("|---|---:|---:|---:|");
        sb.AppendLine($"| {DisplayText.Files} | {summary.AddedFiles} | {summary.RemovedFiles} | {summary.ModifiedFiles} |");
        sb.AppendLine($"| {DisplayText.Registry} | {summary.AddedRegistry} | {summary.RemovedRegistry} | {summary.ModifiedRegistry} |");

        FileSection(sb, diff.FileChanges, ChangeKind.Added);
        FileSection(sb, diff.FileChanges, ChangeKind.Removed);
        FileSection(sb, diff.FileChanges, ChangeKind.Modified);
        RegistrySection(sb, diff.RegistryChanges, ChangeKind.Added);
        RegistrySection(sb, diff.RegistryChanges, ChangeKind.Removed);
        RegistrySection(sb, diff.RegistryChanges, ChangeKind.Modified);

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void FileSection(StringBuilder sb, List<FileChange> changes, ChangeKind kind)
    {
        var rows = changes.Where(c => c.Kind == kind).ToList();
        sb.AppendLine();
        sb.AppendLine($"## 文件 · {DisplayText.Kind(kind)}（{rows.Count} 项）");
        sb.AppendLine();
        if (rows.Count == 0)
        {
            sb.AppendLine("（无）");
            return;
        }

        if (kind == ChangeKind.Modified)
        {
            sb.AppendLine("| 根目录 | 文件 | 旧大小 | 新大小 | 旧修改时间 (UTC) | 新修改时间 (UTC) |");
            sb.AppendLine("|---|---|---:|---:|---|---|");
            foreach (var change in rows)
            {
                sb.AppendLine($"| {EscapeCell(change.Before!.RootPath)} | {EscapeCell(change.After!.RelativePath)} | {FormatUtils.HumanSize(change.Before.Size)} | {FormatUtils.HumanSize(change.After.Size)} | {FormatUtils.FormatUtcTime(change.Before.ModifiedTimeUtc)} | {FormatUtils.FormatUtcTime(change.After.ModifiedTimeUtc)} |");
            }
        }
        else
        {
            sb.AppendLine("| 根目录 | 文件 | 大小 | 修改时间 (UTC) | SHA256 |");
            sb.AppendLine("|---|---|---:|---|---|");
            foreach (var change in rows)
            {
                var entry = change.Main;
                sb.AppendLine($"| {EscapeCell(entry.RootPath)} | {EscapeCell(entry.RelativePath)} | {FormatUtils.HumanSize(entry.Size)} | {FormatUtils.FormatUtcTime(entry.ModifiedTimeUtc)} | {entry.Sha256 ?? "-"} |");
            }
        }
    }

    private static void RegistrySection(StringBuilder sb, List<RegistryChange> changes, ChangeKind kind)
    {
        var rows = changes.Where(c => c.Kind == kind).ToList();
        sb.AppendLine();
        sb.AppendLine($"## 注册表 · {DisplayText.Kind(kind)}（{rows.Count} 项）");
        sb.AppendLine();
        if (rows.Count == 0)
        {
            sb.AppendLine("（无）");
            return;
        }

        if (kind == ChangeKind.Modified)
        {
            sb.AppendLine("| 键路径 | 值名 | 旧类型 | 旧数据 | 新类型 | 新数据 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var change in rows)
            {
                sb.AppendLine($"| {EscapeCell(change.Before!.KeyPath)} | {EscapeCell(DisplayText.ValueName(change.After!))} | {EscapeCell(change.Before.TypeName)} | {EscapeCell(change.Before.Data)} | {EscapeCell(change.After!.TypeName)} | {EscapeCell(change.After.Data)} |");
            }
        }
        else
        {
            sb.AppendLine("| 键路径 | 值名 | 类型 | 数据 |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var change in rows)
            {
                var entry = change.Main;
                sb.AppendLine($"| {EscapeCell(entry.KeyPath)} | {EscapeCell(DisplayText.ValueName(entry))} | {EscapeCell(entry.TypeName)} | {EscapeCell(entry.Data)} |");
            }
        }
    }
}
