using SnapDiff.Core;
using Xunit;

namespace SnapDiff.Tests;

/// <summary>Markdown 导出测试：单元格转义 + 报告结构。</summary>
public class MarkdownExporterTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a|b", "a\\|b")]          // 竖线会切断表格列，必须转义
    [InlineData("l1\nl2", "l1<br>l2")]    // 换行变 <br>
    [InlineData("", "")]
    public void 表格单元格转义(string input, string expected)
        => Assert.Equal(expected, MarkdownExporter.EscapeCell(input));

    [Fact]
    public void 导出包含摘要和各小节()
    {
        var dir = TestPaths.NewTempDir();
        var path = Path.Combine(dir, "report.md");

        var diff = new DiffResult();
        diff.FileChanges.Add(new FileChange(
            ChangeKind.Added,
            null,
            new FileEntry { RootPath = "C:\\Program Files", RelativePath = "App\\new.txt", Size = 2048, ModifiedTimeUtc = DateTime.UtcNow }));
        diff.RegistryChanges.Add(new RegistryChange(
            ChangeKind.Removed,
            new RegistryEntry { KeyPath = "HKCU\\Software\\X", ValueName = "S", TypeName = "REG_SZ", Data = "含|竖线" },
            null));

        MarkdownExporter.Export(diff, path);
        var text = File.ReadAllText(path);

        Assert.Contains("# 快照侦探（SnapDiff）对比报告", text);
        Assert.Contains("## 摘要", text);
        Assert.Contains("## 文件 · 新增（1 项）", text);
        Assert.Contains("new.txt", text);
        Assert.Contains("2 KB", text);
        Assert.Contains("## 注册表 · 删除（1 项）", text);
        Assert.Contains("含\\|竖线", text); // 数据里的竖线已被转义
        Assert.Contains("## 注册表 · 新增（0 项）", text);
        Assert.Contains("（无）", text);
    }
}
